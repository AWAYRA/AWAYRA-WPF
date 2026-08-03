using Awayra.Core.Abstractions;
using Microsoft.Win32;

namespace Awayra.App.Services;

public sealed class DisplayDiagnosticsManager : IDisposable
{
    private static readonly object Sync = new();
    private static DisplayDiagnosticsManager? _current;
    private readonly DisplayDiagnosticsService _recorder;
    private readonly IAppLogger _logger;
    private int _disposed;

    private DisplayDiagnosticsManager(IAppLogger logger)
    {
        _logger = logger;
        _recorder = new DisplayDiagnosticsService(logger);
        _recorder.Start();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanging += OnDisplaySettingsChanging;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanging += OnUserPreferenceChanging;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Record("diagnostics", "system_event_subscriptions_active");
    }

    public static DisplayDiagnosticsManager GetOrCreate(IAppLogger logger)
    {
        lock (Sync)
        {
            return _current ??= new DisplayDiagnosticsManager(logger);
        }
    }

    public string TimelinePath => _recorder.TimelinePath;

    public void Record(string category, string eventName, object? data = null) =>
        _recorder.Record(category, eventName, data);

    public Task<string> CaptureBlinkReportAsync(CancellationToken cancellationToken = default) =>
        _recorder.CaptureBlinkReportAsync(cancellationToken);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanging -= OnDisplaySettingsChanging;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanging -= OnUserPreferenceChanging;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Record("diagnostics", "session_stopping");
        _recorder.Dispose();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) =>
        Record("system_event", "session_switch", new { reason = e.Reason.ToString() });

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) =>
        Record("system_event", "power_mode_changed", new { mode = e.Mode.ToString() });

    private void OnDisplaySettingsChanging(object? sender, EventArgs e) =>
        Record("system_event", "display_settings_changing");

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Record("system_event", "display_settings_changed");

    private void OnUserPreferenceChanging(object sender, UserPreferenceChangingEventArgs e) =>
        Record("system_event", "user_preference_changing", new { category = e.Category.ToString() });

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Record("system_event", "user_preference_changed", new { category = e.Category.ToString() });

    private void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            Dispose();
        }
        catch (Exception ex)
        {
            _logger.Warning($"Display diagnostics shutdown failed: {ex.Message}");
        }
    }
}
