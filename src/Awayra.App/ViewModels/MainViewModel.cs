using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;
using Awayra.Core.Localization;
using Awayra.Core.Models;

namespace Awayra.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ApplicationHost _host;
    private readonly Action _openSettings;
    private readonly DispatcherTimer _uiTimer;
    private readonly SemaphoreSlim _settingsUpdateGate = new(1, 1);
    private readonly DisplayDiagnosticsManager? _displayDiagnostics;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _eyeCountdown = string.Empty;
    [ObservableProperty] private string _moveCountdown = string.Empty;
    [ObservableProperty] private string _eyeStateText = string.Empty;
    [ObservableProperty] private string _moveStateText = string.Empty;
    [ObservableProperty] private string _pauseResumeText = string.Empty;
    [ObservableProperty] private bool _isManuallyPaused;
    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canResume;
    [ObservableProperty] private bool _eyeReminderEnabled;
    [ObservableProperty] private bool _moveReminderEnabled;
    [ObservableProperty] private bool _eyeSoundEnabled;
    [ObservableProperty] private bool _moveSoundEnabled;
    [ObservableProperty] private int _todayEyeCompleted;
    [ObservableProperty] private int _todayMoveCompleted;
    [ObservableProperty] private int _todaySkipped;
    [ObservableProperty] private int _todaySnoozed;
    [ObservableProperty] private string _eyeResetLabel = string.Empty;
    [ObservableProperty] private string _moveBreakLabel = string.Empty;
    [ObservableProperty] private string _settingsLabel = string.Empty;
    [ObservableProperty] private string _eyeResetNowLabel = string.Empty;
    [ObservableProperty] private string _moveBreakNowLabel = string.Empty;
    [ObservableProperty] private string _todayEyeText = string.Empty;
    [ObservableProperty] private string _todayMoveText = string.Empty;
    [ObservableProperty] private string _todaySkippedText = string.Empty;
    [ObservableProperty] private string _todaySnoozedText = string.Empty;
    [ObservableProperty] private string _reportScreenBlinkLabel = "Screen blink happened — Save diagnostics";
    [ObservableProperty] private string _diagnosticStatusText = "Diagnostics record continuously while Awayra is running.";
    [ObservableProperty] private bool _isDiagnosticReportRunning;

    public MainViewModel(ApplicationHost host, Action openSettings)
    {
        _host = host;
        _openSettings = openSettings;
        try
        {
            _displayDiagnostics = DisplayDiagnosticsManager.GetOrCreate(host.Logger);
            _displayDiagnostics.Record("awayra", "dashboard_diagnostics_ready", new
            {
                timeline = _displayDiagnostics.TimelinePath
            });
        }
        catch (Exception ex)
        {
            host.Logger.Error("Display diagnostics could not start", ex);
            DiagnosticStatusText = "Display diagnostics could not start. Check Awayra logs.";
        }

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => Refresh();
        _host.StateChanged += (_, _) => DispatchRefresh();
        Refresh();
        _uiTimer.Start();
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (_host.Scheduler.GetSnapshot().IsPausedManual)
        {
            _host.Scheduler.Resume();
        }
        else
        {
            _host.Scheduler.Pause();
        }

        Refresh();
    }

    [RelayCommand]
    private void EyeNow()
    {
        _displayDiagnostics?.Record("user_action", "eye_break_now_clicked");
        _host.Scheduler.TriggerNow(BreakType.Eye);
        Refresh();
    }

    [RelayCommand]
    private void MoveNow()
    {
        _displayDiagnostics?.Record("user_action", "move_break_now_clicked");
        _host.Scheduler.TriggerNow(BreakType.Move);
        Refresh();
    }

    [RelayCommand]
    private async Task ToggleEyeReminderAsync() =>
        await UpdateQuickSettingAsync(settings => settings.EyeResetEnabled = !settings.EyeResetEnabled).ConfigureAwait(true);

    [RelayCommand]
    private async Task ToggleMoveReminderAsync() =>
        await UpdateQuickSettingAsync(settings => settings.MoveBreakEnabled = !settings.MoveBreakEnabled).ConfigureAwait(true);

    [RelayCommand]
    private async Task ToggleEyeSoundAsync() =>
        await UpdateQuickSettingAsync(settings => settings.EyeBreakSoundEnabled = !settings.EyeBreakSoundEnabled).ConfigureAwait(true);

    [RelayCommand]
    private async Task ToggleMoveSoundAsync() =>
        await UpdateQuickSettingAsync(settings => settings.MoveBreakSoundEnabled = !settings.MoveBreakSoundEnabled).ConfigureAwait(true);

    [RelayCommand]
    private void OpenSettings()
    {
        _displayDiagnostics?.Record("user_action", "settings_clicked");
        _openSettings();
    }

    [RelayCommand(CanExecute = nameof(CanReportScreenBlink))]
    private async Task ReportScreenBlinkAsync()
    {
        if (_displayDiagnostics is null)
        {
            MessageBox.Show(
                "Display diagnostics are not active. Open the Awayra log folder and send awayra.log.",
                "Awayra diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        IsDiagnosticReportRunning = true;
        DiagnosticStatusText = "Collecting the timeline, Windows events, monitor, GPU, power, and DxDiag data...";
        _displayDiagnostics.Record("user_action", "screen_blink_button_clicked");

        try
        {
            var reportPath = await _displayDiagnostics.CaptureBlinkReportAsync().ConfigureAwait(true);
            DiagnosticStatusText = $"Diagnostic ZIP saved: {Path.GetFileName(reportPath)}";
            MessageBox.Show(
                $"The diagnostic ZIP is ready. Send this complete file:\n\n{reportPath}",
                "Awayra display diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RevealFileInExplorer(reportPath);
        }
        catch (Exception ex)
        {
            _host.Logger.Error("Screen blink diagnostic capture failed", ex);
            DiagnosticStatusText = "Diagnostic capture failed. The continuous timeline is still available in LocalAppData\\Awayra\\Diagnostics.";
            MessageBox.Show(
                $"Diagnostic capture failed:\n\n{ex.Message}\n\nThe continuous timeline is still being recorded.",
                "Awayra display diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsDiagnosticReportRunning = false;
        }
    }

    private bool CanReportScreenBlink() => !IsDiagnosticReportRunning;

    partial void OnIsDiagnosticReportRunningChanged(bool value) =>
        ReportScreenBlinkCommand.NotifyCanExecuteChanged();

    private async Task UpdateQuickSettingAsync(Action<AppSettings> update)
    {
        await _settingsUpdateGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var settings = _host.Settings.Copy();
            update(settings);
            await _host.UpdateSettingsAsync(settings).ConfigureAwait(true);
            _displayDiagnostics?.Record("settings", "dashboard_quick_setting_updated", new
            {
                settings.EyeResetEnabled,
                settings.MoveBreakEnabled,
                settings.EyeBreakSoundEnabled,
                settings.MoveBreakSoundEnabled
            });
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Error("Dashboard quick setting update failed", ex);
            Refresh();
        }
        finally
        {
            _settingsUpdateGate.Release();
        }
    }

    private void DispatchRefresh()
    {
        if (_uiTimer.Dispatcher.CheckAccess())
        {
            Refresh();
            return;
        }

        _uiTimer.Dispatcher.Invoke(Refresh);
    }

    public void Refresh()
    {
        var l = _host.Localization;
        var snapshot = _host.Scheduler.GetSnapshot();
        var settings = _host.Settings;
        var today = _host.Statistics.GetToday();

        Title = l.Get(StringKeys.AppTitle);
        StatusText = l.GetStatus(snapshot.Status);
        EyeResetLabel = l.Get(StringKeys.EyeReset);
        MoveBreakLabel = l.Get(StringKeys.MoveBreak);
        SettingsLabel = l.Get(StringKeys.Settings);
        EyeResetNowLabel = l.Get(StringKeys.EyeResetNow);
        MoveBreakNowLabel = l.Get(StringKeys.MoveBreakNow);
        TodayEyeText = $"{l.Get(StringKeys.TodayEyeCompleted)}: {today.EyeCompleted}";
        TodayMoveText = $"{l.Get(StringKeys.TodayMoveCompleted)}: {today.MoveCompleted}";
        TodaySkippedText = $"{l.Get(StringKeys.TodaySkipped)}: {today.Skipped}";
        TodaySnoozedText = $"{l.Get(StringKeys.TodaySnoozed)}: {today.Snoozed}";
        EyeCountdown = snapshot.EyeEnabled ? FormatCountdown(snapshot.EyeRemaining) : "--";
        MoveCountdown = snapshot.MoveEnabled ? FormatCountdown(snapshot.MoveRemaining) : "--";
        EyeStateText = snapshot.EyeEnabled ? l.Get(StringKeys.Enabled) : l.Get(StringKeys.Disabled);
        MoveStateText = snapshot.MoveEnabled ? l.Get(StringKeys.Enabled) : l.Get(StringKeys.Disabled);
        EyeReminderEnabled = snapshot.EyeEnabled;
        MoveReminderEnabled = snapshot.MoveEnabled;
        EyeSoundEnabled = settings.EyeBreakSoundEnabled;
        MoveSoundEnabled = settings.MoveBreakSoundEnabled;
        IsManuallyPaused = snapshot.IsPausedManual;
        PauseResumeText = snapshot.IsPausedManual ? l.Get(StringKeys.Resume) : l.Get(StringKeys.Pause);
        CanPause = !snapshot.IsPausedManual;
        CanResume = snapshot.IsPausedManual;
        TodayEyeCompleted = today.EyeCompleted;
        TodayMoveCompleted = today.MoveCompleted;
        TodaySkipped = today.Skipped;
        TodaySnoozed = today.Snoozed;
        TogglePauseCommand.NotifyCanExecuteChanged();
    }

    private static void RevealFileInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "00:00";
        }

        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }
}
