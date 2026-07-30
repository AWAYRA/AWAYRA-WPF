using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Awayra.Core.Abstractions;
using Awayra.Core.Localization;
using Awayra.App.Views;
using Awayra.Core.Coordination;
using Awayra.Core.Models;
using Awayra.Core.Services;
using Microsoft.Win32;

namespace Awayra.App.Services;

public sealed class OverlayCoordinator
{
    private readonly Func<BreakOverlayWindow> _eyeOverlayFactory;
    private readonly Func<BreakOverlayWindow> _moveOverlayFactory;
    private readonly IAppLogger _logger;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _recoveryTimer;

    private BreakOverlayWindow? _eyeOverlay;
    private BreakOverlayWindow? _moveOverlay;
    private OverlaySessionState _session = OverlaySessionState.Empty;
    private BreakStartedEventArgs? _activeBreakArgs;
    private AppSettings? _activeSettings;
    private LocalizationService? _activeLocalization;
    private TimeSpan? _activeRemaining;
    private int _activeActivityIndex;
    private int _activeGlassClarity;
    private bool _systemRecoveryEventsSubscribed;
    private string _pendingRecoveryReason = "display topology change";

    public OverlayCoordinator(
        Func<BreakOverlayWindow> eyeOverlayFactory,
        Func<BreakOverlayWindow> moveOverlayFactory,
        IAppLogger logger)
    {
        _eyeOverlayFactory = eyeOverlayFactory;
        _moveOverlayFactory = moveOverlayFactory;
        _logger = logger;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _recoveryTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _recoveryTimer.Tick += OnRecoveryTimerTick;
    }

    public bool LastSnapshotCaptured { get; private set; }

    public OverlaySessionState SessionState => _session;

    public void ShowBreak(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(localization);

        UnsubscribeSystemRecoveryEvents();
        _recoveryTimer.Stop();
        CloseWindows();

        _session = OverlaySessionPolicy.AfterCloseAll();
        _activeBreakArgs = args;
        _activeSettings = settings;
        _activeLocalization = localization;
        _activeRemaining = TimeSpan.FromSeconds(args.DurationSeconds);
        _activeActivityIndex = args.ActivityIndex;
        _activeGlassClarity = settings.GlassClarity;

        try
        {
            CreateAndShowActiveOverlay(args, settings, localization);
            _session = OverlaySessionPolicy.AfterShow(args.BreakType, _session);
            SubscribeSystemRecoveryEvents();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to show overlay", ex);
            CloseWindows();
            _session = OverlaySessionPolicy.AfterCloseAll();
            SubscribeSystemRecoveryEvents();
            ScheduleRecovery("initial overlay creation failure");
        }
    }

    public void UpdateActiveBreak(TimeSpan? remaining, LocalizationService localization, int activityIndex)
    {
        _activeRemaining = remaining;
        _activeLocalization = localization;
        _activeActivityIndex = activityIndex;
        ApplyRemainingToActiveWindow();
    }

    public void UpdateGlassClarity(int glassClarity)
    {
        _activeGlassClarity = OverlayGlassSettings.NormalizeGlassClarity(glassClarity);
        _eyeOverlay?.ApplyGlassClarity(_activeGlassClarity);
        _moveOverlay?.ApplyGlassClarity(_activeGlassClarity);
    }

    public bool RecoverActiveOverlay()
    {
        if (_activeBreakArgs is null || _activeSettings is null || _activeLocalization is null)
        {
            return false;
        }

        try
        {
            var activeWindow = _activeBreakArgs.BreakType == BreakType.Eye
                ? _eyeOverlay
                : _moveOverlay;

            if (activeWindow?.TryRecoverOnActiveMonitor() == true)
            {
                activeWindow.ApplyGlassClarity(_activeGlassClarity);
                LastSnapshotCaptured = activeWindow.HasSnapshot;
                ApplyRemainingToActiveWindow();
                _session = OverlaySessionPolicy.AfterShow(
                    _activeBreakArgs.BreakType,
                    OverlaySessionPolicy.AfterCloseAll());
                return true;
            }

            _logger.Warning("Active overlay window was unavailable after a display change; recreating it.");
            CloseWindows();
            _session = OverlaySessionPolicy.AfterCloseAll();
            CreateAndShowActiveOverlay(_activeBreakArgs, _activeSettings, _activeLocalization);
            _session = OverlaySessionPolicy.AfterShow(_activeBreakArgs.BreakType, _session);
            ApplyRemainingToActiveWindow();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to recover active overlay after a display change", ex);
            CloseWindows();
            _session = OverlaySessionPolicy.AfterCloseAll();
            return false;
        }
    }

    public void CloseAll()
    {
        _recoveryTimer.Stop();
        UnsubscribeSystemRecoveryEvents();
        CloseWindows();
        ClearActiveContext();
        _session = OverlaySessionPolicy.AfterCloseAll();
    }

    private void CreateAndShowActiveOverlay(
        BreakStartedEventArgs args,
        AppSettings settings,
        LocalizationService localization)
    {
        if (args.BreakType == BreakType.Eye)
        {
            _eyeOverlay = _eyeOverlayFactory();
            _eyeOverlay.Configure(args, settings, localization, isEye: true);
            _eyeOverlay.ApplyGlassClarity(_activeGlassClarity);
            _eyeOverlay.ShowOnActiveMonitor();
            LastSnapshotCaptured = _eyeOverlay.HasSnapshot;
            return;
        }

        _moveOverlay = _moveOverlayFactory();
        _moveOverlay.Configure(args, settings, localization, isEye: false);
        _moveOverlay.ApplyGlassClarity(_activeGlassClarity);
        _moveOverlay.ShowOnActiveMonitor();
        LastSnapshotCaptured = _moveOverlay.HasSnapshot;
    }

    private void ApplyRemainingToActiveWindow()
    {
        if (_activeBreakArgs?.BreakType == BreakType.Eye)
        {
            _eyeOverlay?.UpdateRemaining(_activeRemaining);
            return;
        }

        if (_activeBreakArgs?.BreakType == BreakType.Move && _activeLocalization is not null)
        {
            _moveOverlay?.UpdateRemaining(_activeRemaining, _activeLocalization, _activeActivityIndex);
        }
    }

    private void CloseWindows()
    {
        if (_eyeOverlay is not null)
        {
            _eyeOverlay.CloseSafely();
            _eyeOverlay = null;
        }

        if (_moveOverlay is not null)
        {
            _moveOverlay.CloseSafely();
            _moveOverlay = null;
        }

        LastSnapshotCaptured = false;
    }

    private void ClearActiveContext()
    {
        _activeBreakArgs = null;
        _activeSettings = null;
        _activeLocalization = null;
        _activeRemaining = null;
        _activeActivityIndex = 0;
        _activeGlassClarity = OverlayGlassSettings.DefaultGlassClarity;
    }

    private void SubscribeSystemRecoveryEvents()
    {
        if (_systemRecoveryEventsSubscribed)
        {
            return;
        }

        try
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _systemRecoveryEventsSubscribed = true;
        }
        catch (Exception ex)
        {
            TryRemoveSystemRecoveryEvents();
            _logger.Warning($"System display recovery events could not be registered: {ex.Message}");
        }
    }

    private void UnsubscribeSystemRecoveryEvents()
    {
        if (!_systemRecoveryEventsSubscribed)
        {
            return;
        }

        TryRemoveSystemRecoveryEvents();
        _systemRecoveryEventsSubscribed = false;
    }

    private static void TryRemoveSystemRecoveryEvents()
    {
        try
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
        catch
        {
            // Process shutdown and desktop teardown can invalidate the SystemEvents window.
        }
    }

    private static void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
    }

    private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
    }

    private static void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
    }

    private void QueueRecovery(string reason)
    {
        if (_activeBreakArgs is null || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            ScheduleRecovery(reason);
            return;
        }

        _dispatcher.BeginInvoke(new Action(() => ScheduleRecovery(reason)));
    }

    private void ScheduleRecovery(string reason)
    {
        if (_activeBreakArgs is null)
        {
            return;
        }

        _pendingRecoveryReason = reason;
        _recoveryTimer.Stop();
        _recoveryTimer.Start();
    }

    private void OnRecoveryTimerTick(object? sender, EventArgs e)
    {
        _recoveryTimer.Stop();
        if (_activeBreakArgs is null)
        {
            return;
        }

        if (RecoverActiveOverlay())
        {
            _logger.Info($"Active overlay recovered after {_pendingRecoveryReason}.");
        }
        else
        {
            _logger.Warning($"Active overlay recovery failed after {_pendingRecoveryReason}.");
        }
    }
}

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly LocalizationService _localization;
    private readonly Action _openDashboard;
    private readonly Action _eyeNow;
    private readonly Action _moveNow;
    private readonly Action _togglePause;
    private readonly Action _openSettings;
    private readonly Action _quit;
    private readonly Func<string> _tooltipProvider;

    public TrayService(
        Icon trayIcon,
        LocalizationService localization,
        Action openDashboard,
        Action eyeNow,
        Action moveNow,
        Action togglePause,
        Action openSettings,
        Action quit,
        Func<string> tooltipProvider)
    {
        _localization = localization;
        _openDashboard = openDashboard;
        _eyeNow = eyeNow;
        _moveNow = moveNow;
        _togglePause = togglePause;
        _openSettings = openSettings;
        _quit = quit;
        _tooltipProvider = tooltipProvider;

        _menu = new ContextMenuStrip();
        _icon = new NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            Text = "Awayra"
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _openDashboard();
            }
        };

        _icon.DoubleClick += (_, _) => _openDashboard();
        RebuildMenu();
    }

    public void UpdateTooltip() => _icon.Text = TrimTooltip(_tooltipProvider());

    private void RebuildMenu()
    {
        _menu.Items.Clear();
        _menu.Items.Add(_localization.Get(StringKeys.OpenAwayra), null, (_, _) => _openDashboard());
        _menu.Items.Add(_localization.Get(StringKeys.EyeResetNow), null, (_, _) => _eyeNow());
        _menu.Items.Add(_localization.Get(StringKeys.MoveBreakNow), null, (_, _) => _moveNow());
        _menu.Items.Add(_localization.Get(StringKeys.TrayPauseReminders), null, (_, _) => _togglePause());
        _menu.Items.Add(_localization.Get(StringKeys.Settings), null, (_, _) => _openSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_localization.Get(StringKeys.Quit), null, (_, _) => _quit());
        _icon.ContextMenuStrip = _menu;
    }

    public void SetPauseMenuLabel(bool paused)
    {
        if (_menu.Items.Count > 3)
        {
            _menu.Items[3].Text = paused
                ? _localization.Get(StringKeys.TrayResumeReminders)
                : _localization.Get(StringKeys.TrayPauseReminders);
        }
    }

    private static string TrimTooltip(string text) =>
        text.Length <= 63 ? text : text[..60] + "...";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
