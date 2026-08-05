namespace Awayra.Core.Models;

public enum BreakType
{
    Eye = 0,
    Move = 1
}

public enum SchedulerStatus
{
    Running = 0,
    PausedManual = 1,
    OutsideWorkHours = 3,
    BreakActive = 4,
    Snoozed = 5,
    Disabled = 6,
    ConfigurationPaused = 7,
    Idle = 8
}
