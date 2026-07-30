using Awayra.Core.Coordination;
using Awayra.Core.Models;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class SystemBootSessionPolicyTests
{
    private static readonly DateTimeOffset Boot = new(2026, 7, 30, 6, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void MissingState_ResetsTimers()
    {
        Assert.IsTrue(SystemBootSessionPolicy.ShouldResetTimers(null, Boot));
    }

    [TestMethod]
    public void SameBoot_DoesNotResetTimers()
    {
        var state = SchedulerState.CreateDefault(Boot.AddHours(1));
        state.SystemBootStartedAtUtc = Boot.AddSeconds(20);

        Assert.IsFalse(SystemBootSessionPolicy.ShouldResetTimers(state, Boot));
    }

    [TestMethod]
    public void DifferentBoot_ResetsTimers()
    {
        var state = SchedulerState.CreateDefault(Boot.AddDays(-2));
        state.SystemBootStartedAtUtc = Boot.AddDays(-2);

        Assert.IsTrue(SystemBootSessionPolicy.ShouldResetTimers(state, Boot));
    }

    [TestMethod]
    public void LegacyStateFromCurrentBoot_DoesNotResetTimers()
    {
        var state = SchedulerState.CreateDefault(Boot.AddHours(1));

        Assert.IsFalse(SystemBootSessionPolicy.ShouldResetTimers(state, Boot));
    }

    [TestMethod]
    public void LegacyStateFromPreviousBoot_ResetsTimers()
    {
        var state = SchedulerState.CreateDefault(Boot.AddHours(-1));

        Assert.IsTrue(SystemBootSessionPolicy.ShouldResetTimers(state, Boot));
    }
}
