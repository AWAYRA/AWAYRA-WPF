using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class SnoozeSemanticsTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 17, 10, 0, 0, TimeSpan.FromHours(4));

    private static (BreakScheduler Scheduler, FakeClock Clock) CreateScheduler(
        DateTimeOffset? start = null,
        AppSettings? settings = null,
        SchedulerState? state = null)
    {
        var clock = new FakeClock(start ?? Start);
        var scheduler = new BreakScheduler(clock, settings ?? AppSettings.CreateDefault(), state);
        return (scheduler, clock);
    }

    private static string FormatCountdown(TimeSpan remaining) =>
        remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";

    [TestMethod]
    public void EyeSnooze_SetsFreshCountdownFromButtonPress()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);

        scheduler.SnoozeActiveBreak();

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.IsNull(snapshot.ActiveBreak);
        Assert.AreEqual("05:00", FormatCountdown(snapshot.EyeRemaining));
        Assert.AreEqual(Start.AddMinutes(5), scheduler.State.EyeNextDue);
        Assert.AreEqual(Start.AddMinutes(5), scheduler.State.EyeSnoozeUntil);
    }

    [TestMethod]
    public void MoveSnooze_SetsFreshCountdownFromButtonPress()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Move);

        scheduler.SnoozeActiveBreak();

        var snapshot = scheduler.GetSnapshot();
        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.IsNull(snapshot.ActiveBreak);
        Assert.AreEqual("05:00", FormatCountdown(snapshot.MoveRemaining));
        Assert.AreEqual(Start.AddMinutes(5), scheduler.State.MoveNextDue);
        Assert.AreEqual(Start.AddMinutes(5), scheduler.State.MoveSnoozeUntil);
    }

    [TestMethod]
    public void RepeatedSnooze_UsesSecondButtonPressTime()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();

        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        scheduler.TriggerNow(BreakType.Eye);
        clock.Advance(TimeSpan.FromMinutes(2));
        scheduler.SnoozeActiveBreak();

        var expectedDue = clock.Now.AddMinutes(5);
        Assert.AreEqual(expectedDue, scheduler.State.EyeNextDue);
        Assert.AreEqual("05:00", FormatCountdown(scheduler.GetSnapshot().EyeRemaining));
    }

    [TestMethod]
    public void RestartDuringActiveSnooze_RecoversRemainingTime()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start.AddMinutes(3);
        state.EyeSnoozeUntil = Start.AddMinutes(3);

        var (scheduler, _) = CreateScheduler(Start.AddMinutes(1), state: state);
        var snapshot = scheduler.GetSnapshot();

        Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
        Assert.AreEqual("02:00", FormatCountdown(snapshot.EyeRemaining));
    }

    [TestMethod]
    public void ClockAdvancingDuringSnooze_DecrementsRemaining()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();

        clock.Advance(TimeSpan.FromMinutes(2));
        scheduler.Tick();

        Assert.AreEqual("03:00", FormatCountdown(scheduler.GetSnapshot().EyeRemaining));
    }

    [TestMethod]
    public void ClockMovingBackwardDuringSnooze_DoesNotProduceNegativeRemaining()
    {
        var (scheduler, clock) = CreateScheduler();
        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();

        clock.Advance(TimeSpan.FromMinutes(-2));
        scheduler.Tick();

        Assert.IsTrue(scheduler.GetSnapshot().EyeRemaining >= TimeSpan.Zero);
    }

    [TestMethod]
    public void EyeSnooze_DoesNotAlterMoveDueTime()
    {
        var (scheduler, clock) = CreateScheduler();
        var moveDue = scheduler.State.MoveNextDue;

        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();

        Assert.AreEqual(moveDue, scheduler.State.MoveNextDue);
        Assert.IsNull(scheduler.State.MoveSnoozeUntil);
    }

    [TestMethod]
    public void MoveSnooze_DoesNotAlterEyeDueTime()
    {
        var (scheduler, clock) = CreateScheduler();
        var eyeDue = scheduler.State.EyeNextDue;

        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();

        Assert.AreEqual(eyeDue, scheduler.State.EyeNextDue);
        Assert.IsNull(scheduler.State.EyeSnoozeUntil);
    }

    [TestMethod]
    public void SnoozeDisabled_PreventsExecution()
    {
        var settings = AppSettings.CreateDefault();
        settings.AllowSnooze = false;
        var (scheduler, _) = CreateScheduler(settings: settings);

        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();

        Assert.AreEqual(BreakType.Eye, scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void DoubleInvocation_IsGuarded()
    {
        var (scheduler, _) = CreateScheduler();
        var endedCount = 0;
        scheduler.BreakEnded += (_, _) => endedCount++;

        scheduler.TriggerNow(BreakType.Eye);
        scheduler.SnoozeActiveBreak();
        scheduler.SnoozeActiveBreak();

        Assert.AreEqual(1, endedCount);
        Assert.AreEqual(SchedulerStatus.Snoozed, scheduler.GetSnapshot().Status);
    }

    [TestMethod]
    public void Snooze_RaisesSingleBreakEndedEvent()
    {
        var (scheduler, _) = CreateScheduler();
        BreakEndedEventArgs? ended = null;
        scheduler.BreakEnded += (_, args) => ended = args;

        scheduler.TriggerNow(BreakType.Move);
        scheduler.SnoozeActiveBreak();

        Assert.IsNotNull(ended);
        Assert.IsTrue(ended.Snoozed);
        Assert.IsFalse(ended.Skipped);
        Assert.IsFalse(ended.Completed);
        Assert.AreEqual(BreakType.Move, ended.BreakType);
    }

    [TestMethod]
    public void EyeSnooze_WhenMoveAlsoDue_DoesNotImmediatelyStartMove()
    {
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        state.MoveNextDue = Start;
        var (scheduler, clock) = CreateScheduler(Start, state: state);
        var started = new List<BreakType>();
        scheduler.BreakStarted += (_, args) => started.Add(args.BreakType);

        scheduler.Tick();
        Assert.AreEqual(BreakType.Eye, started[0]);

        scheduler.SnoozeActiveBreak();
        Assert.AreEqual(SchedulerStatus.Snoozed, scheduler.GetSnapshot().Status);
        Assert.AreEqual(1, started.Count);

        clock.Advance(TimeSpan.FromSeconds(1));
        scheduler.Tick();

        Assert.AreEqual(1, started.Count);
        Assert.IsNull(scheduler.GetSnapshot().ActiveBreak);
    }

    [TestMethod]
    public void EyeSnooze_DoesNotPostponeMoveBreakThatFallsDueDuringTheSnooze()
    {
        var settings = AppSettings.CreateDefault();
        settings.SnoozeDurationMinutes = 30;
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        state.MoveNextDue = Start.AddMinutes(5);
        var (scheduler, clock) = CreateScheduler(Start, settings, state);
        var started = new List<(BreakType Type, DateTimeOffset At)>();
        scheduler.BreakStarted += (_, args) => started.Add((args.BreakType, clock.Now));

        scheduler.Tick();
        scheduler.SnoozeActiveBreak();

        for (var i = 0; i < 12; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            scheduler.Tick();
        }

        var moveStarts = started.Where(s => s.Type == BreakType.Move).ToList();
        Assert.IsTrue(moveStarts.Count > 0, "Move Break never started while Eye Reset was snoozed.");
        Assert.AreEqual(
            Start.AddMinutes(5),
            moveStarts[0].At,
            "A 30-minute Eye Reset snooze must not postpone the independently scheduled Move Break.");
    }

    [TestMethod]
    public void EyeSnooze_AppliesOnlyAShortHandoffGraceToTheMoveBreak()
    {
        var settings = AppSettings.CreateDefault();
        settings.SnoozeDurationMinutes = 30;
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        state.MoveNextDue = Start;
        var (scheduler, clock) = CreateScheduler(Start, settings, state);
        var started = new List<(BreakType Type, DateTimeOffset At)>();
        scheduler.BreakStarted += (_, args) => started.Add((args.BreakType, clock.Now));

        scheduler.Tick();
        scheduler.SnoozeActiveBreak();

        // Still inside the handoff grace: no second overlay is pushed at the user.
        clock.Advance(BreakScheduler.SnoozeHandoffGrace - TimeSpan.FromSeconds(5));
        scheduler.Tick();
        Assert.AreEqual(1, started.Count, "Move Break started inside the snooze handoff grace.");

        // Grace expired: the already-due Move Break is delivered instead of waiting 30 minutes.
        clock.Advance(TimeSpan.FromSeconds(10));
        scheduler.Tick();
        Assert.AreEqual(2, started.Count, "Move Break was not delivered once the handoff grace expired.");
        Assert.AreEqual(BreakType.Move, started[1].Type);
    }

    [TestMethod]
    public void MoveSnooze_DoesNotPostponeEyeResetThatFallsDueDuringTheSnooze()
    {
        var settings = AppSettings.CreateDefault();
        settings.SnoozeDurationMinutes = 30;
        var state = SchedulerState.CreateDefault(Start);
        state.MoveNextDue = Start;
        state.EyeNextDue = Start.AddMinutes(4);
        var (scheduler, clock) = CreateScheduler(Start, settings, state);
        var started = new List<(BreakType Type, DateTimeOffset At)>();
        scheduler.BreakStarted += (_, args) => started.Add((args.BreakType, clock.Now));

        scheduler.Tick();
        Assert.AreEqual(BreakType.Move, started[0].Type);
        scheduler.SnoozeActiveBreak();

        for (var i = 0; i < 10; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            scheduler.Tick();
        }

        var eyeStarts = started.Where(s => s.Type == BreakType.Eye).ToList();
        Assert.IsTrue(eyeStarts.Count > 0, "Eye Reset never started while Move Break was snoozed.");
        Assert.AreEqual(Start.AddMinutes(4), eyeStarts[0].At);
    }

    [TestMethod]
    public void SnoozedBreak_StillWaitsItsOwnFullSnoozeDuration()
    {
        var settings = AppSettings.CreateDefault();
        settings.SnoozeDurationMinutes = 10;
        settings.MoveBreakEnabled = false;
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        var (scheduler, clock) = CreateScheduler(Start, settings, state);
        var started = new List<(BreakType Type, DateTimeOffset At)>();
        scheduler.BreakStarted += (_, args) => started.Add((args.BreakType, clock.Now));

        scheduler.Tick();
        scheduler.SnoozeActiveBreak();

        clock.Advance(TimeSpan.FromMinutes(9));
        scheduler.Tick();
        Assert.AreEqual(1, started.Count, "The snoozed reminder came back before its snooze expired.");

        clock.Advance(TimeSpan.FromMinutes(1));
        scheduler.Tick();
        Assert.AreEqual(2, started.Count);
        Assert.AreEqual(Start.AddMinutes(10), started[1].At);
    }

    [TestMethod]
    public void EyeSnooze_WhenMoveAlsoDue_StartsDueBreakAfterSnoozeExpires()
    {
        var settings = AppSettings.CreateDefault();
        settings.SnoozeDurationMinutes = 5;
        var state = SchedulerState.CreateDefault(Start);
        state.EyeNextDue = Start;
        state.MoveNextDue = Start;
        var (scheduler, clock) = CreateScheduler(Start, settings, state);
        var started = new List<BreakType>();
        scheduler.BreakStarted += (_, args) => started.Add(args.BreakType);

        scheduler.Tick();
        scheduler.SnoozeActiveBreak();

        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();

        Assert.AreEqual(2, started.Count);
        Assert.IsNotNull(scheduler.GetSnapshot().ActiveBreak);
    }
}
