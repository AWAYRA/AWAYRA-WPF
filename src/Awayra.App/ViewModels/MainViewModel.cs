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

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _eyeCountdown = string.Empty;
    [ObservableProperty] private string _moveCountdown = string.Empty;
    [ObservableProperty] private string _eyeStateText = string.Empty;
    [ObservableProperty] private string _moveStateText = string.Empty;
    [ObservableProperty] private string _pauseResumeText = string.Empty;
    [ObservableProperty] private int _todayEyeCompleted;
    [ObservableProperty] private int _todayMoveCompleted;
    [ObservableProperty] private int _todaySkipped;
    [ObservableProperty] private int _todaySnoozed;
    [ObservableProperty] private System.Windows.FlowDirection _flowDirection;

    public MainViewModel(ApplicationHost host, Action openSettings)
    {
        _host = host;
        _openSettings = openSettings;
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => Refresh();
        _host.StateChanged += (_, _) => Refresh();
        _host.Localization.CultureChanged += (_, _) => Refresh();
        Refresh();
        _uiTimer.Start();
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (_host.Scheduler.GetSnapshot().Status == SchedulerStatus.PausedManual)
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

    public void Refresh()
    {
        var l = _host.Localization;
        var snapshot = _host.Scheduler.GetSnapshot();
        var today = _host.Statistics.GetToday();

        Title = l.Get(StringKeys.AppTitle);
        StatusText = l.GetStatus(snapshot.Status);
        EyeCountdown = FormatCountdown(snapshot.EyeRemaining);
        MoveCountdown = FormatCountdown(snapshot.MoveRemaining);
        EyeStateText = snapshot.EyeEnabled ? l.Get(StringKeys.Enabled) : l.Get(StringKeys.Disabled);
        MoveStateText = snapshot.MoveEnabled ? l.Get(StringKeys.Enabled) : l.Get(StringKeys.Disabled);
        PauseResumeText = snapshot.Status == SchedulerStatus.PausedManual ? l.Get(StringKeys.Resume) : l.Get(StringKeys.Pause);
        TodayEyeCompleted = today.EyeCompleted;
        TodayMoveCompleted = today.MoveCompleted;
        TodaySkipped = today.Skipped;
        TodaySnoozed = today.Snoozed;
        FlowDirection = l.CurrentFlowDirection;
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
