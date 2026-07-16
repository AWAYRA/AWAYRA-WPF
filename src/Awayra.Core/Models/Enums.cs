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
    PausedIdle = 2,
    OutsideWorkHours = 3,
    BreakActive = 4,
    Snoozed = 5,
    Disabled = 6
}

public enum AppTheme
{
    Dark = 0,
    Light = 1
}

public enum AppLanguage
{
    Auto = 0,
    English = 1,
    Persian = 2,
    Arabic = 3
}
