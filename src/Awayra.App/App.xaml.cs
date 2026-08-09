using System.Windows;
using System.Windows.Threading;
using Awayra.App.Interop;
using Awayra.App.Services;
using Awayra.App.ViewModels;
using Awayra.App.Views;
using Awayra.Core.Coordination;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;
using Microsoft.Win32;

namespace Awayra.App;

public partial class App : System.Windows.Application, IDisposable
{
    private ApplicationHost? _host;
    private TrayService? _tray;
    private OverlayCoordinator? _overlays;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private MainViewModel? _mainViewModel;
    private NamedPipeSingleInstance? _singleInstance;
    private FileLogger? _logger;
    private bool _isQuitting;
    private UiTestCommandPipe? _uiTestPipe;
    private UiTestDiagnosticsPipe? _uiTestDiagnosticsPipe;
    private SimulatedIdleMonitor? _idleMonitor;
    private DispatcherTimer? _systemTransitionTimer;
    private readonly HashSet<string> _systemTransitionReasons = new(StringComparer.Ordinal);
    private bool _repositionOverlaysAfterTransition;
    private bool _sessionLocked;
    private bool _powerSuspended;
    private bool _disposed;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UiTestMode.Configure(e.Args);
        BuildIdentity.Initialize();

        // Windows Forms hosts the tray icon, so it is told which mode the process is in. Awareness
        // itself comes from app.manifest: WPF has already fixed it by the time OnStartup runs, which
        // is why the runtime call alone left the process DPI-unaware.
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

        AppPaths.EnsureDataRoot();
        _logger = new FileLogger(AppPaths.LogFilePath);
        _logger.Info("Awayra starting.");
        _logger.Info($"DPI awareness: {System.Windows.Forms.Application.HighDpiMode}");
        BuildIdentity.Log(_logger);

        _singleInstance = new NamedPipeSingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            _singleInstance.SignalExistingInstance();
            _logger.Info("Second instance signaled existing instance and exiting.");
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error("Dispatcher unhandled exception", args.Exception);
            if (args.Exception is System.Windows.Markup.XamlParseException && _mainWindow is null)
            {
                _logger?.Error("Dashboard XAML failed to load; tray remains available. Use Open Awayra after fixing resources.");
            }

            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.Error("AppDomain unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        InitializeSystemTransitionTimer();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        var settingsStore = CreateSettingsStore();
        var stateStore = new StateFileStore(new JsonFileStore<SchedulerState>(
            AppPaths.StatePath, _logger, () => SchedulerState.CreateDefault(DateTimeOffset.Now)));
        var statisticsStore = new StatisticsFileStore(new JsonFileStore<StatisticsData>(
            AppPaths.StatisticsPath, _logger, StatisticsData.CreateDefault));

        var localization = new LocalizationService();
        _idleMonitor = new SimulatedIdleMonitor(new WindowsIdleMonitor());
        _host = new ApplicationHost(
            _logger,
            new SystemClock(),
            settingsStore,
            stateStore,
            statisticsStore,
            _idleMonitor,
            new RegistryAutostartService(),
            localization,
            Dispatcher);

        await _host.InitializeAsync().ConfigureAwait(true);
        if (!UiTestMode.IsEnabled)
        {
            _host.ApplyAutostartSetting();
        }
        else
        {
            _logger.Info("UiTest mode: autostart registration skipped.");
        }

        _overlays = new OverlayCoordinator(
            () => new BreakOverlayWindow(_host, new OverlayViewModel(), new MonitorSnapshotService(_logger)),
            () => new BreakOverlayWindow(_host, new OverlayViewModel(), new MonitorSnapshotService(_logger)),
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

        _host.StateChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var snapshot = _host.Scheduler.GetSnapshot();
                _overlays.UpdateActiveBreak(snapshot.ActiveBreakRemaining, _host.Localization, _host.Scheduler.MoveActivityIndex);
                _overlays.UpdateGlassClarity(_host.Settings.GlassClarity);
                UpdateTray();
            });
        };

        _host.GlassClarityPreviewChanged += (_, clarity) =>
        {
            Dispatcher.Invoke(() => _overlays.UpdateGlassClarity(clarity));
        };

        _tray = new TrayService(
            AppIconHelper.ApplicationIcon,
            localization,
            ShowDashboard,
            () => _host.Scheduler.TriggerNow(BreakType.Eye),
            () => _host.Scheduler.TriggerNow(BreakType.Move),
            TogglePause,
            ShowSettings,
            QuitFromTray,
            BuildTrayTooltip);

        var mainViewModel = new MainViewModel(_host, ShowSettings);
        _mainViewModel = mainViewModel;

        // Listening starts only now that ShowDashboard can actually do something. Started earlier it
        // dropped any signal that arrived while the host was still initialising, so relaunching
        // Awayra during startup appeared to do nothing at all.
        _singleInstance.ListenForSignals(ShowDashboard);

        if (!TryCreateMainWindow())
        {
            _logger.Error("Dashboard window creation failed at startup; tray remains available.");
        }
        else if (ApplicationStartupPolicy.ShouldShowDashboardOnStartup(_host.Settings))
        {
            ShowDashboard();
        }

        UpdateTray();
        _logger.Info("Awayra started.");

        if (UiTestMode.IsEnabled)
        {
            UiTestBridge.Register(new UiTestBridge
            {
                ShowDashboard = ShowDashboard,
                ShowSettings = ShowSettings,
                Quit = QuitFromTray,
                TriggerEyeNow = () => _host.Scheduler.TriggerNow(BreakType.Eye),
                TriggerMoveNow = () => _host.Scheduler.TriggerNow(BreakType.Move),
                GetSettings = () => _host.Settings,
                GetEyeCountdown = () => _mainViewModel?.EyeCountdown ?? string.Empty,
                GetMoveCountdown = () => _mainViewModel?.MoveCountdown ?? string.Empty
            });

            _uiTestPipe = new UiTestCommandPipe(DispatchUiTestCommand, _logger);
            _uiTestPipe.Start();

            _uiTestDiagnosticsPipe = new UiTestDiagnosticsPipe(() =>
            {
                var diagnostics = _host.Scheduler.GetDiagnostics();
                diagnostics.SnapshotCaptured = _overlays?.LastSnapshotCaptured ?? false;
                var today = _host.Statistics.GetToday();
                diagnostics.EyeCompleted = today.EyeCompleted;
                diagnostics.MoveCompleted = today.MoveCompleted;
                diagnostics.Skipped = today.Skipped;
                diagnostics.Snoozed = today.Snoozed;
                return diagnostics;
            }, _logger);
            _uiTestDiagnosticsPipe.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _systemTransitionTimer?.Stop();

        // Also runs for exits that never reach QuitFromTray, such as a Windows sign-out. Without it
        // the tray icon survived as a ghost until the user moved the mouse over it.
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tray?.Dispose();
        _host?.Dispose();
        _uiTestPipe?.Dispose();
        _uiTestDiagnosticsPipe?.Dispose();
        _mainViewModel?.Dispose();
        _singleInstance?.Dispose();
        _logger?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void DispatchUiTestCommand(string command)
    {
        Dispatcher.Invoke(() =>
        {
            switch (command.ToUpperInvariant())
            {
                case "TRAY_OPEN":
                case "OPEN_DASHBOARD":
                    ShowDashboard();
                    break;
                case "TRAY_SETTINGS":
                case "OPEN_SETTINGS":
                    ShowSettings();
                    break;
                case "TRAY_QUIT":
                case "QUIT":
                    QuitFromTray();
                    break;
                case "EYE_NOW":
                    _host?.Scheduler.TriggerNow(BreakType.Eye);
                    break;
                case "MOVE_NOW":
                    _host?.Scheduler.TriggerNow(BreakType.Move);
                    break;
                case "SET_IDLE_TRUE":
                case "IDLE_ON":
                    _idleMonitor?.SetSimulatedIdle(true);
                    break;
                case "SET_IDLE_FALSE":
                case "IDLE_OFF":
                    _idleMonitor?.SetSimulatedIdle(false);
                    break;
                case "CLEAR_IDLE_SIMULATION":
                case "IDLE_CLEAR":
                    _idleMonitor?.SetSimulatedIdle(null);
                    break;
            }
        });
    }

    private bool TryCreateMainWindow()
    {
        if (_host is null || _mainViewModel is null)
        {
            return false;
        }

        if (_mainWindow is not null)
        {
            return true;
        }

        try
        {
            var window = new MainWindow(_mainViewModel);
            window.Closing += MainWindow_OnClosing;
            _mainWindow = window;
            MainWindow = window;
            _logger?.Info("Dashboard window created.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to create dashboard window", ex);
            _mainWindow = null;
            return false;
        }
    }

    private void ShowDashboard()
    {
        if (_host is null)
        {
            return;
        }

        void ShowDashboardCore()
        {
            if (!TryCreateMainWindow() || _mainWindow is null)
            {
                return;
            }

            var window = _mainWindow;
            MainWindow = window;

            var presentation = DashboardRestorePlanner.Classify(
                exists: true,
                isVisible: window.IsVisible,
                isMinimized: window.WindowState == WindowState.Minimized);
            var plan = DashboardRestorePlanner.Plan(presentation);

            if (plan.EnsureOnScreen)
            {
                MonitorLocator.EnsureWindowOnScreen(window);
            }

            if (plan.ShowInTaskbar)
            {
                window.ShowInTaskbar = true;
            }

            window.Visibility = Visibility.Visible;

            if (plan.RestoreFromMinimized && window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            if (plan.Show && !window.IsVisible)
            {
                window.Show();
            }

            if (plan.Activate)
            {
                MonitorLocator.ActivateWindow(window);
            }
        }

        if (Dispatcher.CheckAccess())
        {
            ShowDashboardCore();
        }
        else
        {
            Dispatcher.Invoke(ShowDashboardCore);
        }
    }

    private void ShowSettings()
    {
        if (_host is null)
        {
            return;
        }

        if (!TryCreateMainWindow() || _mainWindow is null)
        {
            return;
        }

        void ShowSettingsCore()
        {
            if (_settingsWindow is not null && _settingsWindow.IsVisible)
            {
                MonitorLocator.EnsureWindowOnScreen(_settingsWindow);
                MonitorLocator.ActivateWindow(_settingsWindow);
                return;
            }

            _host.BeginConfigurationSession();
            _settingsWindow = new SettingsWindow(new SettingsViewModel(_host, CloseSettings))
            {
                Owner = _mainWindow
            };
            _settingsWindow.Closed += (_, _) =>
            {
                _host?.EndConfigurationSession(saved: false);
                if (_host is not null)
                {
                    _overlays?.UpdateGlassClarity(_host.Settings.GlassClarity);
                }

                _settingsWindow = null;
                UpdateTray();
            };
            MonitorLocator.EnsureWindowOnScreen(_settingsWindow);
            _settingsWindow.Show();
            MonitorLocator.ActivateWindow(_settingsWindow);
        }

        if (Dispatcher.CheckAccess())
        {
            ShowSettingsCore();
        }
        else
        {
            Dispatcher.Invoke(ShowSettingsCore);
        }
    }

    private void CloseSettings(bool saved)
    {
        if (_host is null)
        {
            return;
        }

        if (!saved)
        {
            _host.EndConfigurationSession(saved: false);
            _overlays?.UpdateGlassClarity(_host.Settings.GlassClarity);
        }

        _settingsWindow?.Close();
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

    private void TogglePause()
    {
        if (_host is null)
        {
            return;
        }

        if (_host.Scheduler.GetSnapshot().IsPausedManual)
        {
            _host.Scheduler.Resume();
        }
        else
        {
            _host.Scheduler.Pause();
        }

        _mainViewModel?.Refresh();
        UpdateTray();
    }

    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isQuitting || _host is null)
        {
            return;
        }

        if (ApplicationStartupPolicy.ShouldHideDashboardToTrayOnClose(_host.Settings, _isQuitting))
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
        _systemTransitionTimer?.Stop();

        _overlays?.CloseAll();
        _tray?.Dispose();
        _host?.Shutdown();

        if (_host is not null)
        {
            await _host.PersistAllAsync().ConfigureAwait(true);
        }

        await (_logger?.FlushAsync() ?? Task.CompletedTask).ConfigureAwait(true);

        // Everything else is released by Dispose, which OnExit calls on the way out.
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

        var formatted = MainViewModel.FormatCountdown(next);
        return $"{status} - {string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _host.Localization.Get(StringKeys.TrayTooltipNextBreak),
            formatted)}";
    }

    private void UpdateTray()
    {
        if (_tray is null || _host is null)
        {
            return;
        }

        _tray.UpdateTooltip();
        _tray.SetPauseMenuLabel(_host.Scheduler.GetSnapshot().IsPausedManual);
    }

    private void InitializeSystemTransitionTimer()
    {
        _systemTransitionTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _systemTransitionTimer.Tick += OnSystemTransitionSettled;
    }

    private void OnSystemTransitionSettled(object? sender, EventArgs e)
    {
        _systemTransitionTimer?.Stop();
        if (_isQuitting || _sessionLocked || _powerSuspended)
        {
            return;
        }

        var reasons = string.Join(", ", _systemTransitionReasons);
        _systemTransitionReasons.Clear();
        var reposition = _repositionOverlaysAfterTransition;
        _repositionOverlaysAfterTransition = false;

        if (!string.IsNullOrWhiteSpace(reasons))
        {
            _logger?.Info($"System transition settled: {reasons}.");
        }

        _host?.CompleteSystemTransition();
        if (reposition)
        {
            _overlays?.RepositionVisibleOverlays();
        }
    }

    private void HoldSystemTransitionCore(string reason)
    {
        _systemTransitionTimer?.Stop();
        _systemTransitionReasons.Add(reason);
        _repositionOverlaysAfterTransition = true;
        _host?.BeginSystemTransition();
    }

    private void ScheduleSystemRecoveryCore(string reason)
    {
        HoldSystemTransitionCore(reason);
        if (_sessionLocked || _powerSuspended || _isQuitting)
        {
            return;
        }

        _systemTransitionTimer?.Start();
    }

    private void RunOnUi(Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, action);
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock)
        {
            RunOnUi(() =>
            {
                _sessionLocked = true;
                HoldSystemTransitionCore("session locked");
            });
        }
        else if (e.Reason is SessionSwitchReason.SessionUnlock)
        {
            RunOnUi(() =>
            {
                _sessionLocked = false;
                ScheduleSystemRecoveryCore("session unlocked");
            });
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend)
        {
            RunOnUi(() =>
            {
                _powerSuspended = true;
                HoldSystemTransitionCore("power suspended");
            });
        }
        else if (e.Mode is PowerModes.Resume)
        {
            RunOnUi(() =>
            {
                _powerSuspended = false;
                ScheduleSystemRecoveryCore("power resumed");
            });
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RunOnUi(() => ScheduleSystemRecoveryCore("display settings changed"));
    }
}
