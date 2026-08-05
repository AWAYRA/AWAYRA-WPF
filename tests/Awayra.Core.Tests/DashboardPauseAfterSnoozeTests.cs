using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class DashboardPauseAfterSnoozeTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 17, 10, 0, 0, TimeSpan.FromHours(4));

    private static (BreakScheduler Scheduler, FakeClock Clock) CreateScheduler(
        AppSettings? settings = null)
    {
        var clock = new FakeClock(Start);
        var scheduler = new BreakScheduler(clock, settings ?? AppSettings.CreateDefault());
        return (scheduler, clock);
    }

    [TestMethod]
    public void EyeSnooze_LeavesManualPauseFalseAndStatusSnoozed()
    {
        var (scheduler, _) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.IsFalse(snapshot.IsPausedManual);
    }

    [TestMethod]
    public void MoveSnooze_LeavesManualPauseFalseAndStatusSnoozed()
    {
        var (scheduler, _) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.IsFalse(snapshot.IsPausedManual);
    }

    [TestMethod]
    public void PauseWhileEyeSnoozed_FreezesSnoozeCountdown()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        var beforePause = scheduler.GetSnapshot().EyeRemaining;

        scheduler.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));
        scheduler.Tick();

        var paused = scheduler.GetSnapshot();

        // An explicit pause outranks a snooze that would lapse on its own, so the dashboard reports
        // the state the user actually chose.
        Assert.AreEqual(SchedulerStatus.PausedManual, paused.Status);
        Assert.IsTrue(paused.IsPausedManual);
        Assert.AreEqual(beforePause.TotalSeconds, paused.EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void ResumeAfterEyeSnoozePause_ContinuesFrozenSnoozeCountdown()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        scheduler.Pause();
        var frozen = scheduler.GetSnapshot().EyeRemaining;

        scheduler.Resume();
        clock.Advance(TimeSpan.FromSeconds(2));
        scheduler.Tick();

        var resumed = scheduler.GetSnapshot();
        Assert.IsFalse(resumed.IsPausedManual);
        Assert.AreEqual(SchedulerStatus.Snoozed, resumed.Status);
        Assert.IsTrue(resumed.EyeRemaining < frozen);
        Assert.IsTrue(resumed.EyeRemaining > frozen - TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void PauseWhileMoveSnoozed_FreezesSnoozeCountdown()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();
        var beforePause = scheduler.GetSnapshot().MoveRemaining;

        scheduler.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));
        scheduler.Tick();

        var paused = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.PausedManual, paused.Status);
        Assert.IsTrue(paused.IsPausedManual);
        Assert.AreEqual(beforePause.TotalSeconds, paused.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void ResumeAfterMoveSnoozePause_ContinuesFrozenSnoozeCountdown()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();
        scheduler.Pause();
        var frozen = scheduler.GetSnapshot().MoveRemaining;

        scheduler.Resume();
        clock.Advance(TimeSpan.FromSeconds(2));
        scheduler.Tick();

        var resumed = scheduler.GetSnapshot();
        Assert.IsFalse(resumed.IsPausedManual);
        Assert.AreEqual(SchedulerStatus.Snoozed, resumed.Status);
        Assert.IsTrue(resumed.MoveRemaining < frozen);
        Assert.IsTrue(resumed.MoveRemaining > frozen - TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void SnoozeStatistics_IncrementOncePerSnooze()
    {
        var clock = new FakeClock(Start);
        var stats = new StatisticsService(clock, new StatisticsData());
        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault());
        var endedCount = 0;
        scheduler.BreakEnded += (_, args) =>
        {
            if (args.Snoozed)
            {
                stats.RecordSnooze();
            }

            endedCount++;
        };

        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();

        Assert.AreEqual(2, endedCount);
        Assert.AreEqual(2, stats.GetToday().Snoozed);
    }
}
