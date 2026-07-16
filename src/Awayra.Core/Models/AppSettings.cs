namespace Awayra.Core.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool EyeResetEnabled { get; set; } = true;
    public int EyeResetIntervalMinutes { get; set; } = 20;
    public int EyeResetDurationSeconds { get; set; } = 20;

    public bool MoveBreakEnabled { get; set; } = true;
    public int MoveBreakIntervalMinutes { get; set; } = 45;
    public int MoveBreakDurationSeconds { get; set; } = 60;

    public bool AllowSkip { get; set; } = true;
    public bool AllowSnooze { get; set; } = true;
    public int SnoozeDurationMinutes { get; set; } = 5;

    public bool PauseWhileIdle { get; set; } = true;
    public int IdleThresholdMinutes { get; set; } = 5;

    public bool WorkHoursEnabled { get; set; }
    public TimeOnly WorkStart { get; set; } = new(9, 0);
    public TimeOnly WorkEnd { get; set; } = new(18, 0);

    public bool RunAtStartup { get; set; }
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;

    public double OverlayOpacity { get; set; } = 0.82;
    public bool ReducedMotion { get; set; }
    public AppLanguage Language { get; set; } = AppLanguage.Auto;
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public static AppSettings CreateDefault() => new();
}
