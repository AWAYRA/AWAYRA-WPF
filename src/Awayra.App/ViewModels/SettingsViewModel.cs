using System.Collections.ObjectModel;
using System.Globalization;
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
    [ObservableProperty] private bool _breakAnimationEnabled;
    [ObservableProperty] private bool _reducedMotion;

    public ObservableCollection<string> ValidationErrors { get; } = [];

    /// <summary>
    /// Fields whose text could not be turned into a number. WPF marks the box red and simply stops
    /// pushing to the source, so without this a typo left the old value in place and Save reported
    /// success while quietly discarding what the user typed.
    /// </summary>
    private readonly SortedSet<string> _unreadableFields = new(StringComparer.Ordinal);

    public bool HasUnreadableFields => _unreadableFields.Count > 0;

    public void SetFieldReadFailure(string propertyName, bool failed)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        var changed = failed
            ? _unreadableFields.Add(propertyName)
            : _unreadableFields.Remove(propertyName);

        if (changed)
        {
            OnPropertyChanged(nameof(HasUnreadableFields));
        }
    }

    internal IReadOnlyCollection<string> UnreadableFields => _unreadableFields;

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

    public bool IsMorningDewSelected
    {
        get => BreakSoundTheme == BreakSoundTheme.MorningDew;
        set
        {
            if (value)
            {
                BreakSoundTheme = BreakSoundTheme.MorningDew;
            }
        }
    }

    public bool IsStillWaterSelected
    {
        get => BreakSoundTheme == BreakSoundTheme.StillWater;
        set
        {
            if (value)
            {
                BreakSoundTheme = BreakSoundTheme.StillWater;
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
        OnPropertyChanged(nameof(IsMorningDewSelected));
        OnPropertyChanged(nameof(IsStillWaterSelected));
    }

    [RelayCommand]
    private void PreviewSound() =>
        _host.BreakSound.Preview(BreakSoundTheme, BreakSoundVolume);

    [RelayCommand]
    private async Task SaveAsync()
    {
        _host.BreakSound.StopPreview();
        var settings = BuildSettings();
        var errors = SettingsValidator.Validate(settings).ToList();

        // An unparseable work-hour string used to fall back to midnight without telling anyone,
        // which silently rewrote the user's schedule. Refuse the save instead.
        if (WorkHoursEnabled && !HasParseableWorkHours())
        {
            errors.Insert(0, "WorkHoursFormatInvalid");
        }

        ValidationErrors.Clear();

        // Reported first, because a field WPF could not read still holds its previous value: every
        // other message below describes what is currently saved, not what is on screen.
        if (_unreadableFields.Count > 0)
        {
            ValidationErrors.Add(string.Format(
                CultureInfo.CurrentCulture,
                _host.Localization.GetValidationMessage("NumericFieldFormatInvalid"),
                string.Join(", ", _unreadableFields.Select(FriendlyFieldName))));
        }

        foreach (var error in errors)
        {
            ValidationErrors.Add(_host.Localization.GetValidationMessage(error));
        }

        if (ValidationErrors.Count > 0)
        {
            return;
        }

        try
        {
            await _host.SaveConfigurationAsync(settings).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Closing here would look exactly like a successful save. Keep the window open and say
            // what went wrong instead.
            _host.Logger.Error("Saving settings failed", ex);
            ValidationErrors.Add(string.Format(
                CultureInfo.CurrentCulture,
                _host.Localization.GetValidationMessage("SettingsSaveFailed"),
                ex.Message));
            return;
        }

        _close(true);
    }

    private static string FriendlyFieldName(string propertyName) => propertyName switch
    {
        nameof(EyeResetIntervalMinutes) => "Eye Reset interval",
        nameof(EyeResetDurationSeconds) => "Eye Reset duration",
        nameof(MoveBreakIntervalMinutes) => "Move Break interval",
        nameof(MoveBreakDurationSeconds) => "Move Break duration",
        nameof(SnoozeDurationMinutes) => "Snooze duration",
        nameof(IdleThresholdMinutes) => "Idle after",
        nameof(BreakSoundRepeatSeconds) => "Sound repeat",
        nameof(BreakSoundVolume) => "Volume",
        _ => propertyName
    };

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
        WorkStart = settings.WorkStart.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        WorkEnd = settings.WorkEnd.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        RunAtStartup = settings.RunAtStartup;
        StartMinimized = settings.StartMinimized;
        CloseToTray = settings.CloseToTray;
        GlassClarity = settings.GlassClarity;
        BreakAnimationEnabled = settings.BreakAnimationEnabled;
        ReducedMotion = settings.ReducedMotion;
    }

    private bool HasParseableWorkHours() =>
        TryParseWorkTime(WorkStart, out _) && TryParseWorkTime(WorkEnd, out _);

    // The field is documented as 24-hour HH:mm, so it is read as such regardless of the machine's
    // time separator, with the local format still accepted for anyone who types it that way.
    private static bool TryParseWorkTime(string value, out TimeOnly parsed) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out parsed) ||
        TimeOnly.TryParse(value, CultureInfo.CurrentCulture, out parsed);

    private AppSettings BuildSettings()
    {
        // When a value cannot be parsed the currently saved time is kept, so a typo never
        // silently rewrites the schedule to midnight.
        var workStart = TryParseWorkTime(WorkStart, out var parsedStart) ? parsedStart : _host.Settings.WorkStart;
        var workEnd = TryParseWorkTime(WorkEnd, out var parsedEnd) ? parsedEnd : _host.Settings.WorkEnd;

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
            BreakAnimationEnabled = BreakAnimationEnabled,
            ReducedMotion = ReducedMotion
        };
    }
}