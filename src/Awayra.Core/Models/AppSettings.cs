namespace Awayra.Core.Models;

using Awayra.Core.Services;

public enum BreakSoundTheme
{
    SoftBell = 0,
    GentleChime = 1,
    CalmDrop = 2,
    CalmPiano = 3,

    /// <summary>Rising and returning pentatonic phrase. Fades in, so it never startles.</summary>
    MorningDew = 4,

    /// <summary>Lower, slower and warmer version of the same rise-and-return shape.</summary>
    StillWater = 5
}

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool EyeResetEnabled { get; set; } = true;
    public int EyeResetIntervalMinutes { get; set; } = 20;
    public int EyeResetDurationSeconds { get; set; } = 20;
    public bool EyeBreakSoundEnabled { get; set; }

    public bool MoveBreakEnabled { get; set; } = true;
    public int MoveBreakIntervalMinutes { get; set; } = 45;
    public int MoveBreakDurationSeconds { get; set; } = 60;
    public bool MoveBreakSoundEnabled { get; set; }

    public BreakSoundTheme BreakSoundTheme { get; set; } = BreakSoundTheme.SoftBell;
    public int BreakSoundVolume { get; set; } = 15;
    public int BreakSoundRepeatSeconds { get; set; } = 2;

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

    public int GlassClarity { get; set; } = OverlayGlassSettings.DefaultGlassClarity;

    /// <summary>
    /// Whether the break overlay shows the guided exercise illustration at all. Independent of
    /// <see cref="ReducedMotion"/>, which keeps the illustration but removes its movement.
    /// </summary>
    public bool BreakAnimationEnabled { get; set; } = true;

    public bool ReducedMotion { get; set; }

    public AppSettings Copy() => (AppSettings)MemberwiseClone();

    public static AppSettings CreateDefault() => new();
}