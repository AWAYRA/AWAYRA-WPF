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

    public MainViewModel(ApplicationHost host, Action openSettings)
    {
        _host = host;
        _openSettings = openSettings;
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
        _host.Scheduler.TriggerNow(BreakType.Eye);
        Refresh();
    }

    [RelayCommand]
    private void MoveNow()
    {
        _host.Scheduler.TriggerNow(BreakType.Move);
        Refresh();
    }

    [RelayCommand]
    private void OpenSettings() => _openSettings();

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
        EyeCountdown = FormatCountdown(snapshot.EyeRemaining);
        MoveCountdown = FormatCountdown(snapshot.MoveRemaining);
        EyeStateText = snapshot.EyeEnabled ? l.Get(StringKeys.Enabled) : l.Get(StringKeys.Disabled);
        MoveStateText = snapshot.MoveEnabled ? l.Get(StringKeys.Enabled) : l.Get(StringKeys.Disabled);
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
