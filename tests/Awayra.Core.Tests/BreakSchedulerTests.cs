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
        Assert.IsNull(snapshot.ActiveBreak);
        Assert.AreEqual(TimeSpan.FromMinutes(5), snapshot.MoveRemaining);
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
    public void Idle_SuppressesNewBreaks()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromMinutes(30));
        scheduler.Tick();

        Assert.AreEqual(SchedulerStatus.Idle, scheduler.GetSnapshot().Status);
        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void Idle_DueTimesMayPassWithoutBacklog()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromHours(2));
        scheduler.Tick();

        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
        Assert.IsNull(scheduler.GetSnapshot().QueuedBreak);
    }

    [TestMethod]
    public void IdleReturn_ResetsFreshEyeInterval()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 30;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(12));
        scheduler.Tick();
        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.SetIdle(false);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(30).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 2);
        Assert.IsNull(snapshot.ActiveBreak);
    }

    [TestMethod]
    public void IdleReturn_ResetsFreshMoveInterval()
    {
        var settings = AppSettings.CreateDefault();
        settings.MoveBreakIntervalMinutes = 45;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromHours(1));
        scheduler.SetIdle(false);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(45).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 2);
    }

    [TestMethod]
    public void IdleReturn_ClearsSnooze()
    {
        var scheduler = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        Assert.AreEqual(SchedulerStatus.Snoozed, scheduler.GetSnapshot().Status);

        scheduler.SetIdle(true);
        scheduler.SetIdle(false);

        Assert.AreNotEqual(SchedulerStatus.Snoozed, scheduler.GetSnapshot().Status);
    }

    [TestMethod]
    public void IdleReturn_SecondSessionResetsAgain()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 20;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        scheduler.SetIdle(false);
        var first = scheduler.GetSnapshot().EyeRemaining;

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();
        scheduler.SetIdle(true);
        scheduler.SetIdle(false);
        var second = scheduler.GetSnapshot().EyeRemaining;

        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, first.TotalSeconds, 2);
        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, second.TotalSeconds, 2);
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
    public void Idle_Enter_CapturesEyeRemaining()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(8));
        scheduler.Tick();
        var beforeIdle = scheduler.GetSnapshot().EyeRemaining;

        scheduler.SetIdle(true);

        Assert.AreEqual(beforeIdle.TotalSeconds, scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void Idle_Enter_CapturesMoveRemaining()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(12));
        scheduler.Tick();
        var beforeIdle = scheduler.GetSnapshot().MoveRemaining;

        scheduler.SetIdle(true);

        Assert.AreEqual(beforeIdle.TotalSeconds, scheduler.GetSnapshot().MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void Idle_EyeDisplayRemainsFrozenThroughMultipleTicks()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(8));
        scheduler.Tick();
        scheduler.SetIdle(true);
        var frozenEye = scheduler.GetSnapshot().EyeRemaining;

        for (var i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(10));
            scheduler.Tick();
            Assert.AreEqual(frozenEye.TotalSeconds, scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
        }
    }

    [TestMethod]
    public void Idle_MoveDisplayRemainsFrozenThroughMultipleTicks()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(12));
        scheduler.Tick();
        scheduler.SetIdle(true);
        var frozenMove = scheduler.GetSnapshot().MoveRemaining;

        for (var i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(10));
            scheduler.Tick();
            Assert.AreEqual(frozenMove.TotalSeconds, scheduler.GetSnapshot().MoveRemaining.TotalSeconds, 1);
        }
    }

    [TestMethod]
    public void Idle_SnoozeDisplayRemainsFrozen()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        scheduler.SetIdle(true);
        var frozenSnooze = scheduler.GetSnapshot().EyeRemaining;

        clock.Advance(TimeSpan.FromMinutes(3));
        scheduler.Tick();

        Assert.AreEqual(frozenSnooze.TotalSeconds, scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void Idle_NoOverlayBecomesActive()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromHours(2));

        for (var i = 0; i < 10; i++)
        {
            scheduler.Tick();
            Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
        }
    }

    [TestMethod]
    public void IdleReturn_DoesNotResumeFromFrozenValues()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 30;
        settings.MoveBreakIntervalMinutes = 45;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(12));
        scheduler.Tick();
        scheduler.SetIdle(true);
        var frozenEye = scheduler.GetSnapshot().EyeRemaining;
        var frozenMove = scheduler.GetSnapshot().MoveRemaining;

        clock.Advance(TimeSpan.FromMinutes(20));
        scheduler.Tick();
        scheduler.SetIdle(false);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreNotEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 5);
        Assert.AreNotEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 5);
        Assert.AreEqual(TimeSpan.FromMinutes(30).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 2);
        Assert.AreEqual(TimeSpan.FromMinutes(45).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 2);
    }

    [TestMethod]
    public void Idle_MultipleTicksDoNotOverwriteCapturedValues()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(7));
        scheduler.Tick();
        scheduler.SetIdle(true);
        var firstCapture = scheduler.GetSnapshot().EyeRemaining;

        clock.Advance(TimeSpan.FromMinutes(30));
        scheduler.Tick();
        var afterLongIdle = scheduler.GetSnapshot().EyeRemaining;

        clock.Advance(TimeSpan.FromSeconds(15));
        scheduler.Tick();
        var afterAnotherTick = scheduler.GetSnapshot().EyeRemaining;

        Assert.AreEqual(firstCapture.TotalSeconds, afterLongIdle.TotalSeconds, 1);
        Assert.AreEqual(firstCapture.TotalSeconds, afterAnotherTick.TotalSeconds, 1);
    }

    [TestMethod]
    public void Idle_SecondSessionCapturesNewRemainingValues()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 20;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        scheduler.SetIdle(true);
        var firstFrozen = scheduler.GetSnapshot().EyeRemaining;
        scheduler.SetIdle(false);

        clock.Advance(TimeSpan.FromMinutes(8));
        scheduler.Tick();
        scheduler.SetIdle(true);
        var secondFrozen = scheduler.GetSnapshot().EyeRemaining;

        Assert.AreNotEqual(firstFrozen.TotalSeconds, secondFrozen.TotalSeconds, 5);
        Assert.IsTrue(secondFrozen > TimeSpan.Zero);
    }

    [TestMethod]
    public void Idle_StatisticsRemainUnchanged()
    {
        var clock = new FakeClock(Start);
        var stats = new StatisticsService(clock);
        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault());

        scheduler.BreakEnded += (_, args) =>
        {
            if (args.Completed)
            {
                stats.RecordCompletion(args.BreakType);
            }
            else if (args.Skipped)
            {
                stats.RecordSkip();
            }
            else if (args.Snoozed)
            {
                stats.RecordSnooze();
            }
        };

        var before = stats.GetToday();
        scheduler.SetIdle(true);
        clock.Advance(TimeSpan.FromHours(2));
        for (var i = 0; i < 20; i++)
        {
            scheduler.Tick();
        }

        var after = stats.GetToday();
        Assert.AreEqual(before.EyeCompleted, after.EyeCompleted);
        Assert.AreEqual(before.MoveCompleted, after.MoveCompleted);
        Assert.AreEqual(before.Skipped, after.Skipped);
        Assert.AreEqual(before.Snoozed, after.Snoozed);
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
    public void RestartRecovery_LongOverdueBreakIsRebasedInsteadOfSeizingTheScreen()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start.AddMinutes(-5);
        var scheduler = CreateScheduler(Start.AddHours(1), state: state);

        scheduler.Tick();

        var snapshot = scheduler.GetSnapshot();
        Assert.IsNull(snapshot.ActiveBreak, "Launching after an absence must not open with a break.");
        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void RestartRecovery_BreakOverdueWithinGraceStillFires()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start.AddSeconds(-30);
        var scheduler = CreateScheduler(Start, state: state);

        scheduler.Tick();

        Assert.AreEqual(BreakType.Eye, scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void RestartRecovery_PersistedActiveBreakIsNotResurrected()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.ActiveBreak = BreakType.Move;
        state.BreakEndsAt = Start.AddSeconds(30);
        var scheduler = CreateScheduler(Start.AddDays(2), state: state);

        var completed = 0;
        scheduler.BreakEnded += (_, args) =>
        {
            if (args.Completed)
            {
                completed++;
            }
        };

        scheduler.Tick();

        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
        Assert.AreEqual(0, completed, "A break the process never showed must not be credited as completed.");
    }

    [TestMethod]
    public void RebaseOverdueSchedules_LeavesFutureSchedulesAlone()
    {
        var scheduler = CreateScheduler();
        var before = scheduler.GetSnapshot();

        scheduler.RebaseOverdueSchedules();

        var after = scheduler.GetSnapshot();
        Assert.AreEqual(before.EyeRemaining, after.EyeRemaining);
        Assert.AreEqual(before.MoveRemaining, after.MoveRemaining);
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
    public void ManualPause_FreezesCountdowns()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var beforeEye = scheduler.GetSnapshot().EyeRemaining;
        var beforeMove = scheduler.GetSnapshot().MoveRemaining;

        scheduler.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));
        scheduler.Tick();

        var paused = scheduler.GetSnapshot();
        Assert.AreEqual(beforeEye.TotalSeconds, paused.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(beforeMove.TotalSeconds, paused.MoveRemaining.TotalSeconds, 1);
        Assert.AreEqual(SchedulerStatus.PausedManual, paused.Status);

        scheduler.Resume();
        clock.Advance(TimeSpan.FromSeconds(2));
        scheduler.Tick();
        var resumed = scheduler.GetSnapshot();
        Assert.IsTrue(resumed.EyeRemaining < beforeEye);
        Assert.IsTrue(resumed.EyeRemaining > beforeEye - TimeSpan.FromSeconds(35));
    }

    [TestMethod]
    public void WorkHoursOutside_FreezesDisplayedCountdown()
    {
        var settings = AppSettings.CreateDefault();
        settings.WorkHoursEnabled = true;
        settings.WorkStart = new TimeOnly(9, 0);
        settings.WorkEnd = new TimeOnly(18, 0);
        var start = new DateTimeOffset(2026, 7, 17, 17, 30, 0, TimeSpan.FromHours(4));
        var scheduler = CreateScheduler(start, settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        clock.Advance(TimeSpan.FromMinutes(45));
        scheduler.Tick();

        var outside = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.OutsideWorkHours, outside.Status);
        var frozenEye = outside.EyeRemaining;

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();
        var stillOutside = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, stillOutside.EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void ConfigurationPause_FreezesCountdowns()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var before = scheduler.GetSnapshot();

        scheduler.EnterConfigurationPause();
        clock.Advance(TimeSpan.FromSeconds(40));
        scheduler.Tick();

        var paused = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.ConfigurationPaused, paused.Status);
        Assert.AreEqual(before.EyeRemaining.TotalSeconds, paused.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(before.MoveRemaining.TotalSeconds, paused.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void ConfigurationSave_WithUnchangedSchedule_ResumesFrozenCountdowns()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 1;
        settings.MoveBreakIntervalMinutes = 1;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        clock.Advance(TimeSpan.FromSeconds(20));
        scheduler.Tick();
        scheduler.EnterConfigurationPause();
        var frozenEye = scheduler.GetSnapshot().EyeRemaining;
        var frozenMove = scheduler.GetSnapshot().MoveRemaining;
        clock.Advance(TimeSpan.FromSeconds(40));
        var saveTime = clock.Now;
        scheduler.ApplyConfigurationSave(settings, saveTime);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 2);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 2);
        Assert.AreEqual(SchedulerStatus.Running, snapshot.Status);
    }

    [TestMethod]
    public void ConfigurationSave_WithChangedSchedule_ResetsFullIntervalsFromSaveTime()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 1;
        settings.MoveBreakIntervalMinutes = 1;
        var scheduler = CreateScheduler(settings: settings);
        var clock = GetClock(scheduler);

        scheduler.EnterConfigurationPause();
        clock.Advance(TimeSpan.FromSeconds(40));
        var changed = AppSettings.CreateDefault();
        changed.EyeResetIntervalMinutes = 2;
        changed.MoveBreakIntervalMinutes = 2;
        var saveTime = clock.Now;
        scheduler.ApplyConfigurationSave(changed, saveTime);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(2).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 2);
        Assert.AreEqual(TimeSpan.FromMinutes(2).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 2);
        Assert.AreEqual(SchedulerStatus.Running, snapshot.Status);
    }

    [TestMethod]
    public void ConfigurationCancel_ResumesFrozenCountdowns()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var before = scheduler.GetSnapshot();

        scheduler.EnterConfigurationPause();
        clock.Advance(TimeSpan.FromSeconds(40));
        scheduler.CancelConfigurationPause();

        var resumed = scheduler.GetSnapshot();
        Assert.IsTrue(resumed.EyeRemaining <= before.EyeRemaining);
        Assert.IsTrue(resumed.EyeRemaining >= before.EyeRemaining - TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void ConfigurationPause_BlocksTriggerNow()
    {
        var scheduler = CreateScheduler();
        scheduler.EnterConfigurationPause();
        scheduler.TriggerNow(BreakType.Eye);
        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void ReEnableEye_StartsFreshInterval()
    {
        var scheduler = CreateScheduler();
        var clock = GetClock(scheduler);
        var disabled = AppSettings.CreateDefault();
        disabled.EyeResetEnabled = false;
        scheduler.UpdateSettings(disabled);
        clock.Advance(TimeSpan.FromMinutes(30));
        scheduler.Tick();

        var enabled = AppSettings.CreateDefault();
        enabled.EyeResetEnabled = true;
        scheduler.UpdateSettings(enabled);

        Assert.AreEqual(TimeSpan.FromMinutes(20), scheduler.GetSnapshot().EyeRemaining);
    }
}
