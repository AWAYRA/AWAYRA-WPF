using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class BreakSchedulerTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 17, 10, 0, 0, TimeSpan.FromHours(4));

    private static BreakScheduler CreateScheduler(DateTimeOffset? start = null, AppSettings? settings = null, SchedulerState? state = null)
    {
        var clock = new FakeClock(start ?? Start);
        return new BreakScheduler(clock, settings ?? AppSettings.CreateDefault(), state);
    }

    private static FakeClock GetClock(BreakScheduler scheduler)
    {
        var field = typeof(BreakScheduler).GetField("_clock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (FakeClock)field!.GetValue(scheduler)!;
    }

    [TestMethod]
    public void DefaultSchedules_UseConfiguredIntervals()
    {
        var scheduler = CreateScheduler();
        var snapshot = scheduler.GetSnapshot();

        Assert.AreEqual(TimeSpan.FromMinutes(20), snapshot.EyeRemaining);
        Assert.AreEqual(TimeSpan.FromMinutes(45), snapshot.MoveRemaining);
        Assert.AreEqual(SchedulerStatus.Running, snapshot.Status);
    }

    [TestMethod]
    public void IndependentTimers_DecrementSeparately()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(10), snapshot.EyeRemaining);
        Assert.AreEqual(TimeSpan.FromMinutes(35), snapshot.MoveRemaining);
    }

    [TestMethod]
    public void Completion_SchedulesNextFromCompletionTime()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(20));
        scheduler.Tick();
        Assert.AreEqual(BreakType.Eye, scheduler.GetSnapshot().ActiveBreak);

        clock.Advance(TimeSpan.FromSeconds(20));
        scheduler.Tick();

        var snapshot = scheduler.GetSnapshot();
        Assert.IsNull(snapshot.ActiveBreak);
        Assert.AreEqual(TimeSpan.FromMinutes(20), snapshot.EyeRemaining);
    }

    [TestMethod]
    public void Skip_SchedulesNextInterval()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SkipActiveBreak();

        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
        Assert.IsTrue(scheduler.GetSnapshot().EyeRemaining > TimeSpan.Zero);
    }

    [TestMethod]
    public void Snooze_DelaysReminders()
    {
        var scheduler = CreateScheduler();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
    }

    [TestMethod]
    public void ManualPause_FreezesDelivery()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.Pause();
        clock.Advance(TimeSpan.FromHours(2));
        scheduler.Tick();

        Assert.AreEqual(SchedulerStatus.PausedManual, scheduler.GetSnapshot().Status);
        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void IdlePause_SuppressesNewBreaks()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromMinutes(30));
        scheduler.Tick();

        Assert.AreEqual(SchedulerStatus.PausedIdle, scheduler.GetSnapshot().Status);
        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void IdleReturn_DoesNotBurstMultipleOverdue()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromHours(2));
        scheduler.SetIdle(false);
        scheduler.Tick();

        var snapshot = scheduler.GetSnapshot();
        Assert.IsTrue(snapshot.ActiveBreak is null || snapshot.QueuedBreak is null);
    }

    [TestMethod]
    public void ClockForwardJump_MarksBreakDue()
    {
        var state = SchedulerState.CreateDefault(Start);
        var scheduler = CreateScheduler(Start, state: state);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromHours(3));
        scheduler.Tick();

        Assert.IsNotNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void ClockBackwardJump_ClampedWithoutNegativeRemaining()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start.AddMinutes(5);
        var scheduler = CreateScheduler(Start, state: state);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(-10));
        scheduler.Tick();

        Assert.IsTrue(scheduler.GetSnapshot().EyeRemaining >= TimeSpan.Zero);
    }

    [TestMethod]
    public void BothDueSimultaneously_PrioritizesOlderAndQueuesSecond()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        state.MoveNextDue = Start;
        var scheduler = CreateScheduler(Start, state: state);

        scheduler.Tick();

        var snapshot = scheduler.GetSnapshot();
        Assert.IsNotNull(snapshot.ActiveBreak);
        Assert.IsNotNull(snapshot.QueuedBreak);
        Assert.AreNotEqual(snapshot.ActiveBreak, snapshot.QueuedBreak);
    }

    [TestMethod]
    public void QueuedBreak_StartsAfterFirstCompletes()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        state.MoveNextDue = Start;
        var scheduler = CreateScheduler(Start, state: state);
        var clock = GetClock(scheduler);

        scheduler.Tick();
        scheduler.CompleteActiveBreak();
        scheduler.Tick();

        Assert.IsNotNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void IntervalChange_UpdatesLiveSchedule()
    {
        var scheduler = CreateScheduler();
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 10;
        scheduler.UpdateSettings(settings);

        Assert.AreEqual(TimeSpan.FromMinutes(10), scheduler.GetSnapshot().EyeRemaining);
    }

    [TestMethod]
    public void RestartRecovery_OverdueBreakStartsOnTick()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start.AddMinutes(-5);
        var scheduler = CreateScheduler(Start.AddHours(1), state: state);

        scheduler.Tick();
        Assert.AreEqual(BreakType.Eye, scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void DisabledReminder_DoesNotStart()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetEnabled = false;
        settings.MoveBreakEnabled = false;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromHours(2));
        scheduler.Tick();

        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void InvalidInterval_RejectsSettingsUpdate()
    {
        var scheduler = CreateScheduler();
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 0;

        Assert.ThrowsExactly<InvalidOperationException>(() => scheduler.UpdateSettings(settings));
    }

    [TestMethod]
    public void Resume_RestoresRunningStatus()
    {
        var scheduler = CreateScheduler();
        scheduler.Pause();
        scheduler.Resume();

        Assert.AreEqual(SchedulerStatus.Running, scheduler.GetSnapshot().Status);
    }

    [TestMethod]
    public void WorkHours_OutsideRange_SuppressesBreaks()
    {
        var settings = AppSettings.CreateDefault();
        settings.WorkHoursEnabled = true;
        settings.WorkStart = new TimeOnly(9, 0);
        settings.WorkEnd = new TimeOnly(18, 0);
        var scheduler = CreateScheduler(new DateTimeOffset(2026, 7, 17, 20, 0, 0, TimeSpan.FromHours(4)), settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromHours(2));
        scheduler.Tick();

        Assert.AreEqual(SchedulerStatus.OutsideWorkHours, scheduler.GetSnapshot().Status);
    }
}
