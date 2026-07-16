using System.Windows;
using System.Windows.Threading;
using Awayra.App.Interop;
using Awayra.App.Services;
using Awayra.App.ViewModels;
using Awayra.App.Views;
using Awayra.Core.Abstractions;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;
using Microsoft.Win32;

namespace Awayra.App;

public partial class App : System.Windows.Application
{
    private ApplicationHost? _host;
    private TrayService? _tray;
    private OverlayCoordinator? _overlays;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private NamedPipeSingleInstance? _singleInstance;
    private FileLogger? _logger;
    private bool _isQuitting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        AppPaths.EnsureDataRoot();
        _logger = new FileLogger(AppPaths.LogFilePath);
        _logger.Info("Awayra starting.");

        _singleInstance = new NamedPipeSingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            _singleInstance.SignalExistingInstance();
            _logger.Info("Second instance signaled existing instance and exiting.");
            Shutdown(0);
            return;
        }

        _singleInstance.ListenForSignals(ShowDashboard);

        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error("Dispatcher unhandled exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.Error("AppDomain unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        var settingsStore = CreateSettingsStore();
        var stateStore = new StateFileStore(new JsonFileStore<SchedulerState>(
            AppPaths.StatePath, _logger, () => SchedulerState.CreateDefault(DateTimeOffset.Now)));
        var statisticsStore = new StatisticsFileStore(new JsonFileStore<StatisticsData>(
            AppPaths.StatisticsPath, _logger, StatisticsData.CreateDefault));

        var localization = new LocalizationService();
        _host = new ApplicationHost(
            _logger,
            new SystemClock(),
            settingsStore,
            stateStore,
            statisticsStore,
            new WindowsIdleMonitor(),
            new RegistryAutostartService(),
            localization);

        await _host.InitializeAsync().ConfigureAwait(true);
        _host.ApplyAutostartSetting();

        _overlays = new OverlayCoordinator(
            () => new BreakOverlayWindow(_host, new OverlayViewModel()),
            () => new BreakOverlayWindow(_host, new OverlayViewModel()),
            _logger);

        _host.Scheduler.BreakStarted += (_, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                _overlays.ShowBreak(args, _host.Settings, _host.Localization);
                UpdateTray();
            });
        };

        _host.Scheduler.BreakEnded += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _overlays.CloseAll();
                UpdateTray();
            });
        };

        _host.Scheduler.SnapshotChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var snapshot = _host.Scheduler.GetSnapshot();
                _overlays.UpdateActiveBreak(snapshot.ActiveBreakRemaining, _host.Localization, _host.Scheduler.MoveActivityIndex);
                UpdateTray();
            });
        };

        _host.StateChanged += (_, _) => Dispatcher.Invoke(UpdateTray);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "awayra.ico");
        var trayIcon = File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;

        _tray = new TrayService(
            trayIcon,
            localization,
            ShowDashboard,
            () => _host.Scheduler.TriggerNow(BreakType.Eye),
            () => _host.Scheduler.TriggerNow(BreakType.Move),
            TogglePause,
            ShowSettings,
            QuitFromTray,
            BuildTrayTooltip);

        var mainViewModel = new MainViewModel(_host, ShowSettings);
        _mainWindow = new MainWindow(mainViewModel);
        _mainWindow.Closing += MainWindow_OnClosing;

        if (!_host.Settings.StartMinimized)
        {
            ShowDashboard();
        }

        UpdateTray();
        _logger.Info("Awayra started.");
    }

    private SettingsFileStore CreateSettingsStore()
    {
        var store = new JsonFileStore<AppSettings>(
            AppPaths.SettingsPath,
            _logger!,
            AppSettings.CreateDefault,
            json => SettingsRecovery.LoadWithRecovery(json));
        return new SettingsFileStore(store);
    }

    private void ShowDashboard()
    {
        if (_mainWindow is null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            if (_mainWindow.IsVisible)
            {
                MonitorLocator.ActivateWindow(_mainWindow);
            }
            else
            {
                _mainWindow.Show();
                MonitorLocator.ActivateWindow(_mainWindow);
            }
        });
    }

    private void ShowSettings()
    {
        if (_host is null || _mainWindow is null)
        {
            return;
        }

        if (_settingsWindow is not null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(new SettingsViewModel(_host, () => _settingsWindow?.Close()))
        {
            Owner = _mainWindow
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void TogglePause()
    {
        if (_host is null)
        {
            return;
        }

        if (_host.Scheduler.GetSnapshot().Status == SchedulerStatus.PausedManual)
        {
            _host.Scheduler.Resume();
        }
        else
        {
            _host.Scheduler.Pause();
        }

        UpdateTray();
    }

    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isQuitting || _host is null)
        {
            return;
        }

        if (_host.Settings.CloseToTray)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
        }
    }

    private async void QuitFromTray()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        _logger?.Info("Quit requested from tray.");

        _overlays?.CloseAll();
        _tray?.Dispose();
        _host?.Shutdown();

        if (_host is not null)
        {
            await _host.PersistAllAsync().ConfigureAwait(true);
        }

        await (_logger?.FlushAsync() ?? Task.CompletedTask).ConfigureAwait(true);
        _singleInstance?.Release();
        Shutdown(0);
    }

    private string BuildTrayTooltip()
    {
        if (_host is null)
        {
            return "Awayra";
        }

        var snapshot = _host.Scheduler.GetSnapshot();
        var status = _host.Localization.GetStatus(snapshot.Status);
        if (snapshot.NextBreakDue is null)
        {
            return status;
        }

        var next = snapshot.EyeEnabled && snapshot.MoveEnabled
            ? (snapshot.EyeRemaining <= snapshot.MoveRemaining ? snapshot.EyeRemaining : snapshot.MoveRemaining)
            : snapshot.EyeEnabled ? snapshot.EyeRemaining : snapshot.MoveRemaining;

        var formatted = next.TotalHours >= 1
            ? $"{(int)next.TotalHours:D2}:{next.Minutes:D2}:{next.Seconds:D2}"
            : $"{next.Minutes:D2}:{next.Seconds:D2}";

        return $"{status} - {string.Format(_host.Localization.Get(StringKeys.TrayTooltipNextBreak), formatted)}";
    }

    private void UpdateTray()
    {
        if (_tray is null || _host is null)
        {
            return;
        }

        _tray.UpdateTooltip();
        _tray.SetPauseMenuLabel(_host.Scheduler.GetSnapshot().Status == SchedulerStatus.PausedManual);
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock)
        {
            _host?.Scheduler.Tick();
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Resume)
        {
            _host?.Scheduler.Tick();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Active overlay reposition handled on next show.
    }
}
