namespace Awayra.Core.Models;

public sealed class SchedulerSnapshot
{
    public SchedulerStatus Status { get; init; }
    public bool IsPausedManual { get; init; }
    public TimeSpan EyeRemaining { get; init; }
    public TimeSpan MoveRemaining { get; init; }
    public bool EyeEnabled { get; init; }
    public bool MoveEnabled { get; init; }
    public BreakType? ActiveBreak { get; init; }
    public BreakType? QueuedBreak { get; init; }
    public TimeSpan? ActiveBreakRemaining { get; init; }
    public DateTimeOffset? NextBreakDue { get; init; }
}

public sealed class BreakStartedEventArgs : EventArgs
{
    public required BreakType BreakType { get; init; }
    public required int DurationSeconds { get; init; }
    public required int ActivityIndex { get; init; }
}

public sealed class BreakEndedEventArgs : EventArgs
{
    public required BreakType BreakType { get; init; }
    public required bool Completed { get; init; }
    public required bool Skipped { get; init; }
    public required bool Snoozed { get; init; }
}
