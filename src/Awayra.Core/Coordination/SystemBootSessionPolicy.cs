using Awayra.Core.Models;

namespace Awayra.Core.Coordination;

public static class SystemBootSessionPolicy
{
    private static readonly TimeSpan SameBootTolerance = TimeSpan.FromMinutes(2);

    public static bool ShouldResetTimers(
        SchedulerState? persistedState,
        DateTimeOffset currentBootStartedAtUtc)
    {
        if (persistedState is null)
        {
            return true;
        }

        if (persistedState.SystemBootStartedAtUtc is { } previousBootStartedAtUtc)
        {
            return (currentBootStartedAtUtc - previousBootStartedAtUtc).Duration() > SameBootTolerance;
        }

        if (persistedState.LastClockCheck == default)
        {
            return true;
        }

        // Migration path for state files created before boot identity was persisted.
        return currentBootStartedAtUtc > persistedState.LastClockCheck + SameBootTolerance;
    }
}
