namespace Awayra.Core.Models;

public sealed class SchedulerState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset EyeNextDue { get; set; }
    public DateTimeOffset MoveNextDue { get; set; }

    public bool IsPausedManual { get; set; }

    public BreakType? ActiveBreak { get; set; }
    public BreakType? QueuedBreak { get; set; }
    public DateTimeOffset? BreakEndsAt { get; set; }
    public DateTimeOffset? SnoozeUntil { get; set; }
    public DateTimeOffset? EyeSnoozeUntil { get; set; }
    public DateTimeOffset? MoveSnoozeUntil { get; set; }

    public DateTimeOffset LastClockCheck { get; set; }

    public DateTimeOffset? EyeLastCompleted { get; set; }
    public DateTimeOffset? MoveLastCompleted { get; set; }

    public static SchedulerState CreateDefault(DateTimeOffset now) => new()
    {
        EyeNextDue = now.AddMinutes(AppSettings.CreateDefault().EyeResetIntervalMinutes),
        MoveNextDue = now.AddMinutes(AppSettings.CreateDefault().MoveBreakIntervalMinutes),
        LastClockCheck = now
    };
}
