using Awayra.Core.Models;

namespace Awayra.Core.Services;

public sealed class SchedulerDiagnostics
{
    public SchedulerStatus Status { get; set; }
    public int EyeRemainingSeconds { get; set; }
    public int MoveRemainingSeconds { get; set; }
    public DateTimeOffset EyeNextDue { get; set; }
    public DateTimeOffset MoveNextDue { get; set; }
    public DateTimeOffset? EyeSnoozeUntil { get; set; }
    public DateTimeOffset? MoveSnoozeUntil { get; set; }
    public bool IsPausedManual { get; set; }
    public bool IsIdlePaused { get; set; }
    public bool IsConfigurationPaused { get; set; }
    public bool IsOutsideWorkHours { get; set; }
    public BreakType? ActiveBreak { get; set; }
    public BreakType? QueuedBreak { get; set; }
    public int GlassClarity { get; set; }
    public double BackgroundTintOpacity { get; set; }
    public double BlurRadius { get; set; }
    public bool SnapshotCaptured { get; set; }
    public int EyeCompleted { get; set; }
    public int MoveCompleted { get; set; }
    public int Skipped { get; set; }
    public int Snoozed { get; set; }
    public double IdleSeconds { get; set; }
}
