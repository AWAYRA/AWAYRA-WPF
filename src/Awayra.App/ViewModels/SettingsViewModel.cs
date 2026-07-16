using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ApplicationHost _host;
    private readonly Action _close;

    [ObservableProperty] private bool _eyeResetEnabled;
    [ObservableProperty] private int _eyeResetIntervalMinutes;
    [ObservableProperty] private int _eyeResetDurationSeconds;
    [ObservableProperty] private bool _moveBreakEnabled;
    [ObservableProperty] private int _moveBreakIntervalMinutes;
    [ObservableProperty] private int _moveBreakDurationSeconds;
    [ObservableProperty] private bool _allowSkip;
    [ObservableProperty] private bool _allowSnooze;
    [ObservableProperty] private int _snoozeDurationMinutes;
    [ObservableProperty] private bool _pauseWhileIdle;
    [ObservableProperty] private int _idleThresholdMinutes;
    [ObservableProperty] private bool _workHoursEnabled;
    [ObservableProperty] private string _workStart = "09:00";
    [ObservableProperty] private string _workEnd = "18:00";
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private double _overlayOpacity;
    [ObservableProperty] private bool _reducedMotion;
    [ObservableProperty] private AppLanguage _selectedLanguage;
    [ObservableProperty] private System.Windows.FlowDirection _flowDirection;

    public ObservableCollection<string> ValidationErrors { get; } = [];

    public SettingsViewModel(ApplicationHost host, Action close)
    {
        _host = host;
        _close = close;
        LoadFromSettings(host.Settings);
        _host.Localization.CultureChanged += (_, _) => FlowDirection = host.Localization.CurrentFlowDirection;
        FlowDirection = host.Localization.CurrentFlowDirection;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = BuildSettings();
        var errors = SettingsValidator.Validate(settings);
        ValidationErrors.Clear();
        foreach (var error in errors)
        {
            ValidationErrors.Add(_host.Localization.GetValidationMessage(error));
        }

        if (errors.Count > 0)
        {
            return;
        }

        await _host.UpdateSettingsAsync(settings).ConfigureAwait(true);
        _close();
    }

    [RelayCommand]
    private void Cancel() => _close();

    partial void OnEyeResetEnabledChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnEyeResetIntervalMinutesChanged(int value) => _ = TryAutoSaveAsync();
    partial void OnEyeResetDurationSecondsChanged(int value) => _ = TryAutoSaveAsync();
    partial void OnMoveBreakEnabledChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnMoveBreakIntervalMinutesChanged(int value) => _ = TryAutoSaveAsync();
    partial void OnMoveBreakDurationSecondsChanged(int value) => _ = TryAutoSaveAsync();
    partial void OnAllowSkipChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnAllowSnoozeChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnSnoozeDurationMinutesChanged(int value) => _ = TryAutoSaveAsync();
    partial void OnPauseWhileIdleChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnIdleThresholdMinutesChanged(int value) => _ = TryAutoSaveAsync();
    partial void OnWorkHoursEnabledChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnWorkStartChanged(string value) => _ = TryAutoSaveAsync();
    partial void OnWorkEndChanged(string value) => _ = TryAutoSaveAsync();
    partial void OnRunAtStartupChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnStartMinimizedChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnCloseToTrayChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnOverlayOpacityChanged(double value) => _ = TryAutoSaveAsync();
    partial void OnReducedMotionChanged(bool value) => _ = TryAutoSaveAsync();
    partial void OnSelectedLanguageChanged(AppLanguage value) => _ = TryAutoSaveAsync();

    private async Task TryAutoSaveAsync()
    {
        var settings = BuildSettings();
        if (!SettingsValidator.IsValid(settings))
        {
            return;
        }

        await _host.UpdateSettingsAsync(settings).ConfigureAwait(true);
    }

    private void LoadFromSettings(AppSettings settings)
    {
        EyeResetEnabled = settings.EyeResetEnabled;
        EyeResetIntervalMinutes = settings.EyeResetIntervalMinutes;
        EyeResetDurationSeconds = settings.EyeResetDurationSeconds;
        MoveBreakEnabled = settings.MoveBreakEnabled;
        MoveBreakIntervalMinutes = settings.MoveBreakIntervalMinutes;
        MoveBreakDurationSeconds = settings.MoveBreakDurationSeconds;
        AllowSkip = settings.AllowSkip;
        AllowSnooze = settings.AllowSnooze;
        SnoozeDurationMinutes = settings.SnoozeDurationMinutes;
        PauseWhileIdle = settings.PauseWhileIdle;
        IdleThresholdMinutes = settings.IdleThresholdMinutes;
        WorkHoursEnabled = settings.WorkHoursEnabled;
        WorkStart = settings.WorkStart.ToString("HH:mm");
        WorkEnd = settings.WorkEnd.ToString("HH:mm");
        RunAtStartup = settings.RunAtStartup;
        StartMinimized = settings.StartMinimized;
        CloseToTray = settings.CloseToTray;
        OverlayOpacity = settings.OverlayOpacity;
        ReducedMotion = settings.ReducedMotion;
        SelectedLanguage = settings.Language;
    }

    private AppSettings BuildSettings()
    {
        TimeOnly.TryParse(WorkStart, out var workStart);
        TimeOnly.TryParse(WorkEnd, out var workEnd);

        return new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            EyeResetEnabled = EyeResetEnabled,
            EyeResetIntervalMinutes = EyeResetIntervalMinutes,
            EyeResetDurationSeconds = EyeResetDurationSeconds,
            MoveBreakEnabled = MoveBreakEnabled,
            MoveBreakIntervalMinutes = MoveBreakIntervalMinutes,
            MoveBreakDurationSeconds = MoveBreakDurationSeconds,
            AllowSkip = AllowSkip,
            AllowSnooze = AllowSnooze,
            SnoozeDurationMinutes = SnoozeDurationMinutes,
            PauseWhileIdle = PauseWhileIdle,
            IdleThresholdMinutes = IdleThresholdMinutes,
            WorkHoursEnabled = WorkHoursEnabled,
            WorkStart = workStart,
            WorkEnd = workEnd,
            RunAtStartup = RunAtStartup,
            StartMinimized = StartMinimized,
            CloseToTray = CloseToTray,
            OverlayOpacity = OverlayOpacity,
            ReducedMotion = ReducedMotion,
            Language = SelectedLanguage,
            Theme = AppTheme.Dark
        };
    }
}
