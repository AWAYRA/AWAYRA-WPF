using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

/// <summary>
/// Covers the states Awayra can only reach by being away: shut down, suspended, locked, or paused
/// while the user took a break by hand. Each of these used to hand the user a break they had not
/// earned, or a schedule that had quietly rewound.
/// </summary>
[TestClass]
public sealed class SchedulerRecoveryTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 5, 9, 0, 0, TimeSpan.FromHours(4));

    [TestMethod]
    public void LaunchAfterMultiDayGap_DoesNotOpenWithABreak()
    {
        var clock = new FakeClock(Start);
        var persisted = new SchedulerState
        {
            EyeNextDue = Start.AddDays(-3).AddMinutes(20),
            MoveNextDue = Start.AddDays(-3).AddMinutes(45),
            LastClockCheck = Start.AddDays(-3)
        };

        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault(), persisted);
        BreakStartedEventArgs? started = null;
        scheduler.BreakStarted += (_, e) => started = e;

        scheduler.Tick();

        Assert.IsNull(started, "A three-day absence must not be treated as three days of screen time.");
        var snapshot = scheduler.GetSnapshot();
        Assert.IsNull(snapshot.QueuedBreak);
        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(TimeSpan.FromMinutes(45).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void RebaseOverdueSchedules_ClearsASnoozeThatExpiredWhileAway()
    {
        var clock = new FakeClock(Start);
        var persisted = new SchedulerState
        {
            EyeNextDue = Start.AddHours(-2),
            EyeSnoozeUntil = Start.AddHours(-2),
            MoveNextDue = Start.AddMinutes(30),
            LastClockCheck = Start.AddHours(-2)
        };

        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault(), persisted);

        Assert.AreEqual(SchedulerStatus.Running, scheduler.GetSnapshot().Status);
    }

    [TestMethod]
    public void RebaseOverdueSchedules_AfterSuspendGap_PostponesInsteadOfFiring()
    {
        var clock = new FakeClock(Start);
        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault());

        // The tick loop is suspended across a sleep, so the clock moves without any tick observing it.
        clock.Advance(TimeSpan.FromHours(8));
        scheduler.RebaseOverdueSchedules();
        scheduler.Tick();

        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void ManualBreakWhilePaused_SurvivesResumeWithTheNewInterval()
    {
        var clock = new FakeClock(Start);
        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault());

        clock.Advance(TimeSpan.FromMinutes(19));
        scheduler.Pause();
        Assert.AreEqual(TimeSpan.FromMinutes(1).TotalSeconds, scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);

        scheduler.TriggerNow(BreakType.Eye);
        clock.Advance(TimeSpan.FromSeconds(20));
        scheduler.CompleteActiveBreak();
        scheduler.Resume();

        // Resuming used to restore the one minute frozen before the break, firing a second break a
        // minute after the user had just taken one.
        Assert.AreEqual(
            TimeSpan.FromMinutes(20).TotalSeconds,
            scheduler.GetSnapshot().EyeRemaining.TotalSeconds,
            1);
    }

    [TestMethod]
    public void ManualBreakWhileIdle_SurvivesReturnWithTheNewInterval()
    {
        var clock = new FakeClock(Start);
        var settings = AppSettings.CreateDefault();
        var scheduler = new BreakScheduler(clock, settings);

        clock.Advance(TimeSpan.FromMinutes(19));
        scheduler.SetIdle(true);
        scheduler.TriggerNow(BreakType.Move);
        clock.Advance(TimeSpan.FromSeconds(60));
        scheduler.CompleteActiveBreak();

        Assert.AreEqual(
            TimeSpan.FromMinutes(45).TotalSeconds,
            scheduler.GetSnapshot().MoveRemaining.TotalSeconds,
            1);
    }

    [TestMethod]
    public void FirstMoveBreak_ShowsTheFirstActivity()
    {
        var clock = new FakeClock(Start);
        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault());
        var seen = new List<int>();
        scheduler.BreakStarted += (_, e) =>
        {
            if (e.BreakType == BreakType.Move)
            {
                seen.Add(e.ActivityIndex);
            }
        };

        for (var i = 0; i < BreakScheduler.MoveActivityCount + 1; i++)
        {
            scheduler.TriggerNow(BreakType.Move);
            Assert.AreEqual(seen[^1], scheduler.MoveActivityIndex, "The published index must match the break on screen.");
            clock.Advance(TimeSpan.FromSeconds(60));
            scheduler.CompleteActiveBreak();
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 0 }, seen);
    }

    [TestMethod]
    public void MoveActivityIndex_IsUsableBeforeTheFirstMoveBreak()
    {
        var scheduler = new BreakScheduler(new FakeClock(Start), AppSettings.CreateDefault());

        Assert.AreEqual(0, scheduler.MoveActivityIndex);
    }
}
