using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class SettingsSaveTransitionTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 17, 10, 0, 0, TimeSpan.FromHours(4));

    private static (BreakScheduler Scheduler, FakeClock Clock, AppSettings Settings) CreateScenario(
        AppSettings? settings = null)
    {
        var effectiveSettings = settings ?? AppSettings.CreateDefault();
        var clock = new FakeClock(Start);
        var scheduler = new BreakScheduler(clock, effectiveSettings);
        return (scheduler, clock, effectiveSettings);
    }

    private static AppSettings CloneSettings(AppSettings source) =>
        new()
        {
            SchemaVersion = source.SchemaVersion,
            EyeResetEnabled = source.EyeResetEnabled,
            EyeResetIntervalMinutes = source.EyeResetIntervalMinutes,
            EyeResetDurationSeconds = source.EyeResetDurationSeconds,
            MoveBreakEnabled = source.MoveBreakEnabled,
            MoveBreakIntervalMinutes = source.MoveBreakIntervalMinutes,
            MoveBreakDurationSeconds = source.MoveBreakDurationSeconds,
            AllowSkip = source.AllowSkip,
            AllowSnooze = source.AllowSnooze,
            SnoozeDurationMinutes = source.SnoozeDurationMinutes,
            PauseWhileIdle = source.PauseWhileIdle,
            IdleThresholdMinutes = source.IdleThresholdMinutes,
            WorkHoursEnabled = source.WorkHoursEnabled,
            WorkStart = source.WorkStart,
            WorkEnd = source.WorkEnd,
            RunAtStartup = source.RunAtStartup,
            StartMinimized = source.StartMinimized,
            CloseToTray = source.CloseToTray,
            GlassClarity = source.GlassClarity,
            BreakAnimationEnabled = source.BreakAnimationEnabled,
            ReducedMotion = source.ReducedMotion
        };

    private static (TimeSpan FrozenEye, TimeSpan FrozenMove) PauseAdvanceAndCapture(
        BreakScheduler scheduler,
        FakeClock clock,
        TimeSpan advance)
    {
        scheduler.EnterConfigurationPause();
        var frozenEye = scheduler.GetSnapshot().EyeRemaining;
        var frozenMove = scheduler.GetSnapshot().MoveRemaining;
        clock.Advance(advance);
        scheduler.Tick();
        return (frozenEye, frozenMove);
    }

    [TestMethod]
    public void SaveWithNoChanges_PreservesEyeRemainingTime()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, _) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(40));

        scheduler.ApplyConfigurationSave(CloneSettings(settings), clock.Now);

        Assert.AreEqual(frozenEye.TotalSeconds, scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void SaveWithNoChanges_PreservesMoveRemainingTime()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (_, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(40));

        scheduler.ApplyConfigurationSave(CloneSettings(settings), clock.Now);

        Assert.AreEqual(frozenMove.TotalSeconds, scheduler.GetSnapshot().MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void SaveWithNoChanges_DoesNotSubtractTimeSpentInSettings()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromMinutes(2));

        scheduler.ApplyConfigurationSave(CloneSettings(settings), clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void EyeOnlyIntervalChange_ResetsEyeOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.EyeResetIntervalMinutes = 25;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(25).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.AreNotEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 5);
    }

    [TestMethod]
    public void EyeOnlyDurationChange_ResetsEyeOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.EyeResetDurationSeconds = 30;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.AreNotEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 5);
    }

    [TestMethod]
    public void MoveOnlyIntervalChange_ResetsMoveOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.MoveBreakIntervalMinutes = 50;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(TimeSpan.FromMinutes(50).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.AreNotEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 5);
    }

    [TestMethod]
    public void MoveOnlyDurationChange_ResetsMoveOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.MoveBreakDurationSeconds = 90;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(TimeSpan.FromMinutes(45).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.AreNotEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 5);
    }

    [TestMethod]
    public void BothScheduleFieldsChanged_ResetsBoth()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 1;
        settings.MoveBreakIntervalMinutes = 1;
        var (scheduler, clock, baseSettings) = CreateScenario(settings);
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(40));

        var updated = CloneSettings(baseSettings);
        updated.EyeResetIntervalMinutes = 2;
        updated.MoveBreakIntervalMinutes = 2;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(2).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 2);
        Assert.AreEqual(TimeSpan.FromMinutes(2).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 2);
        Assert.AreNotEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 5);
        Assert.AreNotEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 5);
    }

    [TestMethod]
    public void GlassOnlyChange_ResetsNeither()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.GlassClarity = 120;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void IdleThresholdOnlyChange_ResetsNeither()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.IdleThresholdMinutes = 10;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void WorkHoursOnlyChange_ResetsNeither()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.WorkHoursEnabled = true;
        updated.WorkStart = new TimeOnly(8, 0);
        updated.WorkEnd = new TimeOnly(17, 0);
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void ReducedMotionOnlyChange_ResetsNeither()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.ReducedMotion = true;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void EyeDisable_AffectsEyeOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.EyeResetEnabled = false;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.Zero, snapshot.EyeRemaining);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.IsNull(scheduler.State.EyeSnoozeUntil);
    }

    [TestMethod]
    public void MoveDisable_AffectsMoveOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var (frozenEye, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        var updated = CloneSettings(settings);
        updated.MoveBreakEnabled = false;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(TimeSpan.Zero, snapshot.MoveRemaining);
        Assert.IsNull(scheduler.State.MoveSnoozeUntil);
    }

    [TestMethod]
    public void UnchangedEyeSnooze_ResumesFrozenRemainingTime()
    {
        var (scheduler, clock, settings) = CreateScenario();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        var (frozenEye, _) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        scheduler.ApplyConfigurationSave(CloneSettings(settings), clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.IsNotNull(scheduler.State.EyeSnoozeUntil);
    }

    [TestMethod]
    public void UnchangedMoveSnooze_ResumesFrozenRemainingTime()
    {
        var (scheduler, clock, settings) = CreateScenario();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();
        var (_, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(30));

        scheduler.ApplyConfigurationSave(CloneSettings(settings), clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.IsNotNull(scheduler.State.MoveSnoozeUntil);
    }

    [TestMethod]
    public void ChangedEyeScheduling_ClearsEyeSnoozeOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();
        var frozenMove = scheduler.GetSnapshot().MoveRemaining;
        PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(20));

        var updated = CloneSettings(settings);
        updated.EyeResetIntervalMinutes = 18;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.IsNull(scheduler.State.EyeSnoozeUntil);
        Assert.AreEqual(TimeSpan.FromMinutes(18).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.IsNotNull(scheduler.State.MoveSnoozeUntil);
    }

    [TestMethod]
    public void ChangedMoveScheduling_ClearsMoveSnoozeOnly()
    {
        var (scheduler, clock, settings) = CreateScenario();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();
        var frozenEye = scheduler.GetSnapshot().EyeRemaining;
        PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(20));

        var updated = CloneSettings(settings);
        updated.MoveBreakIntervalMinutes = 40;
        scheduler.ApplyConfigurationSave(updated, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(frozenEye.TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.IsNotNull(scheduler.State.EyeSnoozeUntil);
        Assert.IsNull(scheduler.State.MoveSnoozeUntil);
        Assert.AreEqual(TimeSpan.FromMinutes(40).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void SaveWithNoChanges_PreservesStatistics()
    {
        var clock = new FakeClock(Start);
        var stats = new StatisticsService(clock, new StatisticsData());
        var scheduler = new BreakScheduler(clock, AppSettings.CreateDefault());
        scheduler.BreakEnded += (_, args) =>
        {
            if (args.Completed)
            {
                stats.RecordCompletion(args.BreakType);
            }
            else if (args.Snoozed)
            {
                stats.RecordSnooze();
            }
            else if (args.Skipped)
            {
                stats.RecordSkip();
            }
        };

        scheduler.TriggerNow(BreakType.Eye);
        stats.RecordCompletion(BreakType.Eye);
        var before = stats.GetToday();

        clock.Advance(TimeSpan.FromMinutes(3));
        scheduler.Tick();
        scheduler.EnterConfigurationPause();
        clock.Advance(TimeSpan.FromSeconds(45));
        scheduler.ApplyConfigurationSave(AppSettings.CreateDefault(), clock.Now);

        var after = stats.GetToday();
        Assert.AreEqual(before.EyeCompleted, after.EyeCompleted);
        Assert.AreEqual(before.MoveCompleted, after.MoveCompleted);
        Assert.AreEqual(before.Skipped, after.Skipped);
        Assert.AreEqual(before.Snoozed, after.Snoozed);
    }

    [TestMethod]
    public void CancelBehavior_RemainsUnchanged()
    {
        var (scheduler, clock, _) = CreateScenario();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        var before = scheduler.GetSnapshot();

        scheduler.EnterConfigurationPause();
        clock.Advance(TimeSpan.FromSeconds(40));
        scheduler.CancelConfigurationPause();

        var resumed = scheduler.GetSnapshot();
        Assert.IsTrue(resumed.EyeRemaining <= before.EyeRemaining);
        Assert.IsTrue(resumed.EyeRemaining >= before.EyeRemaining - TimeSpan.FromSeconds(5));
        Assert.AreEqual(SchedulerStatus.Running, resumed.Status);
    }

    [TestMethod]
    public void EyeReEnable_StartsFreshIntervalWhileMoveResumes()
    {
        var (scheduler, clock, settings) = CreateScenario();
        var disabled = CloneSettings(settings);
        disabled.EyeResetEnabled = false;
        scheduler.UpdateSettings(disabled);
        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();
        var (_, frozenMove) = PauseAdvanceAndCapture(scheduler, clock, TimeSpan.FromSeconds(20));

        var reenabled = CloneSettings(settings);
        reenabled.EyeResetEnabled = true;
        scheduler.ApplyConfigurationSave(reenabled, clock.Now);

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }
}
