using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;
using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ApplicationHost _host;
    private readonly Action<bool> _close;

    [ObservableProperty] private bool _eyeResetEnabled;
    [ObservableProperty] private int _eyeResetIntervalMinutes;
    [ObservableProperty] private int _eyeResetDurationSeconds;
    [ObservableProperty] private bool _eyeBreakSoundEnabled;
    [ObservableProperty] private bool _moveBreakEnabled;
    [ObservableProperty] private int _moveBreakIntervalMinutes;
    [ObservableProperty] private int _moveBreakDurationSeconds;
    [ObservableProperty] private bool _moveBreakSoundEnabled;
    [ObservableProperty] private BreakSoundTheme _breakSoundTheme;
    [ObservableProperty] private int _breakSoundVolume;
    [ObservableProperty] private int _breakSoundRepeatSeconds;
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
    [ObservableProperty] private int _glassClarity;
    [ObservableProperty] private bool _reducedMotion;

    public ObservableCollection<string> ValidationErrors { get; } = [];

    public bool IsSoftBellSelected
    {
        get => BreakSoundTheme == BreakSoundTheme.SoftBell;
        set
        {
            if (value)
            {
                BreakSoundTheme = BreakSoundTheme.SoftBell;
            }
        }
    }

    public bool IsGentleChimeSelected
    {
        get => BreakSoundTheme == BreakSoundTheme.GentleChime;
        set
        {
            if (value)
            {
                BreakSoundTheme = BreakSoundTheme.GentleChime;
            }
        }
    }

    public bool IsCalmDropSelected
    {
        get => BreakSoundTheme == BreakSoundTheme.CalmDrop;
        set
        {
            if (value)
            {
                BreakSoundTheme = BreakSoundTheme.CalmDrop;
            }
        }
    }

    public bool IsCalmPianoSelected
    {
        get => BreakSoundTheme == BreakSoundTheme.CalmPiano;
        set
        {
            if (value)
            {
                BreakSoundTheme = BreakSoundTheme.CalmPiano;
            }
        }
    }

    public SettingsViewModel(ApplicationHost host, Action<bool> close)
    {
        _host = host;
        _close = close;
        LoadFromSettings(host.Settings);
    }

    partial void OnGlassClarityChanged(int value) => _host.PreviewGlassClarity(value);

    partial void OnBreakSoundThemeChanged(BreakSoundTheme value)
    {
        OnPropertyChanged(nameof(IsSoftBellSelected));
        OnPropertyChanged(nameof(IsGentleChimeSelected));
        OnPropertyChanged(nameof(IsCalmDropSelected));
        OnPropertyChanged(nameof(IsCalmPianoSelected));
    }

    [RelayCommand]
    private void PreviewSound() =>
        _host.BreakSound.Preview(BreakSoundTheme, BreakSoundVolume);

    [RelayCommand]
    private async Task SaveAsync()
    {
        _host.BreakSound.StopPreview();
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

        await _host.SaveConfigurationAsync(settings).ConfigureAwait(true);
        _close(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _host.BreakSound.StopPreview();
        _close(false);
    }

    private void LoadFromSettings(AppSettings settings)
    {
        EyeResetEnabled = settings.EyeResetEnabled;
        EyeResetIntervalMinutes = settings.EyeResetIntervalMinutes;
        EyeResetDurationSeconds = settings.EyeResetDurationSeconds;
        EyeBreakSoundEnabled = settings.EyeBreakSoundEnabled;
        MoveBreakEnabled = settings.MoveBreakEnabled;
        MoveBreakIntervalMinutes = settings.MoveBreakIntervalMinutes;
        MoveBreakDurationSeconds = settings.MoveBreakDurationSeconds;
        MoveBreakSoundEnabled = settings.MoveBreakSoundEnabled;
        BreakSoundTheme = settings.BreakSoundTheme;
        BreakSoundVolume = settings.BreakSoundVolume;
        BreakSoundRepeatSeconds = settings.BreakSoundRepeatSeconds;
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
        GlassClarity = settings.GlassClarity;
        ReducedMotion = settings.ReducedMotion;
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
            EyeBreakSoundEnabled = EyeBreakSoundEnabled,
            MoveBreakEnabled = MoveBreakEnabled,
            MoveBreakIntervalMinutes = MoveBreakIntervalMinutes,
            MoveBreakDurationSeconds = MoveBreakDurationSeconds,
            MoveBreakSoundEnabled = MoveBreakSoundEnabled,
            BreakSoundTheme = BreakSoundTheme,
            BreakSoundVolume = BreakSoundVolume,
            BreakSoundRepeatSeconds = BreakSoundRepeatSeconds,
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
            GlassClarity = GlassClarity,
            ReducedMotion = ReducedMotion,
            Theme = AppTheme.Dark
        };
    }
}