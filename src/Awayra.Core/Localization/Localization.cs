namespace Awayra.Core.Localization;

public static class StringKeys
{
    public const string AppTitle = nameof(AppTitle);
    public const string StatusRunning = nameof(StatusRunning);
    public const string StatusPaused = nameof(StatusPaused);
    public const string StatusIdle = nameof(StatusIdle);
    public const string StatusConfigurationPaused = nameof(StatusConfigurationPaused);
    public const string StatusOutsideWorkHours = nameof(StatusOutsideWorkHours);
    public const string StatusBreakActive = nameof(StatusBreakActive);
    public const string StatusSnoozed = nameof(StatusSnoozed);
    public const string StatusDisabled = nameof(StatusDisabled);

    public const string EyeReset = nameof(EyeReset);
    public const string MoveBreak = nameof(MoveBreak);
    public const string Enabled = nameof(Enabled);
    public const string Disabled = nameof(Disabled);
    public const string Pause = nameof(Pause);
    public const string Resume = nameof(Resume);
    public const string EyeResetNow = nameof(EyeResetNow);
    public const string MoveBreakNow = nameof(MoveBreakNow);
    public const string Settings = nameof(Settings);
    public const string Quit = nameof(Quit);
    public const string OpenAwayra = nameof(OpenAwayra);

    public const string TodayEyeCompleted = nameof(TodayEyeCompleted);
    public const string TodayMoveCompleted = nameof(TodayMoveCompleted);
    public const string TodaySkipped = nameof(TodaySkipped);
    public const string TodaySnoozed = nameof(TodaySnoozed);

    public const string EyeResetInstructionDistance = nameof(EyeResetInstructionDistance);
    public const string EyeResetInstructionBlink = nameof(EyeResetInstructionBlink);
    public const string Skip = nameof(Skip);
    public const string Snooze = nameof(Snooze);
    public const string Complete = nameof(Complete);
    public const string SoundMuted = nameof(SoundMuted);
    public const string SoundOn = nameof(SoundOn);

    public const string MoveActivityStand = nameof(MoveActivityStand);
    public const string MoveActivityWalk = nameof(MoveActivityWalk);
    public const string MoveActivityShoulders = nameof(MoveActivityShoulders);
    public const string MoveActivityNeck = nameof(MoveActivityNeck);
    public const string MoveActivityStretch = nameof(MoveActivityStretch);

    public const string ValidationEyeResetIntervalInvalid = nameof(ValidationEyeResetIntervalInvalid);
    public const string ValidationEyeResetDurationInvalid = nameof(ValidationEyeResetDurationInvalid);
    public const string ValidationMoveBreakIntervalInvalid = nameof(ValidationMoveBreakIntervalInvalid);
    public const string ValidationMoveBreakDurationInvalid = nameof(ValidationMoveBreakDurationInvalid);
    public const string ValidationSnoozeDurationInvalid = nameof(ValidationSnoozeDurationInvalid);
    public const string ValidationIdleThresholdInvalid = nameof(ValidationIdleThresholdInvalid);
    public const string ValidationGlassClarityInvalid = nameof(ValidationGlassClarityInvalid);
    public const string ValidationWorkHoursRangeInvalid = nameof(ValidationWorkHoursRangeInvalid);
    public const string TrayTooltipNextBreak = nameof(TrayTooltipNextBreak);
    public const string TrayPauseReminders = nameof(TrayPauseReminders);
    public const string TrayResumeReminders = nameof(TrayResumeReminders);

    public static IReadOnlyList<string> All { get; } =
    [
        AppTitle, StatusRunning, StatusPaused, StatusIdle, StatusConfigurationPaused,
        StatusOutsideWorkHours, StatusBreakActive, StatusSnoozed, StatusDisabled, EyeReset, MoveBreak,
        Enabled, Disabled, Pause, Resume, EyeResetNow, MoveBreakNow, Settings, Quit, OpenAwayra,
        TodayEyeCompleted, TodayMoveCompleted, TodaySkipped, TodaySnoozed, EyeResetInstructionDistance,
        EyeResetInstructionBlink, Skip, Snooze, Complete, SoundMuted, SoundOn, MoveActivityStand,
        MoveActivityWalk, MoveActivityShoulders, MoveActivityNeck, MoveActivityStretch,
        ValidationEyeResetIntervalInvalid, ValidationEyeResetDurationInvalid,
        ValidationMoveBreakIntervalInvalid, ValidationMoveBreakDurationInvalid,
        ValidationSnoozeDurationInvalid, ValidationIdleThresholdInvalid, ValidationGlassClarityInvalid,
        ValidationWorkHoursRangeInvalid, TrayTooltipNextBreak, TrayPauseReminders, TrayResumeReminders
    ];
}
