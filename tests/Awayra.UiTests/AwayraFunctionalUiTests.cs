using Awayra.Core.Models;
using Awayra.UiTests.Support;
using FlaUI.Core.AutomationElements;

namespace Awayra.UiTests;

[TestClass]
public sealed class AwayraFunctionalUiTests
{
    private static UiAutomationSession? _session;
    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("Awayra"))
        {
            try { process.Kill(true); } catch { }
        }
    }

    [TestInitialize]
    public void TestInit()
    {
        EnsureNoAwayraProcesses(TimeSpan.FromSeconds(5));

        _session = new UiAutomationSession();
        _session.BeginTest(TestContext.TestName);
        try
        {
            _session.Launch("Debug");
        }
        catch
        {
            _session.SaveFailureArtifacts(TestContext.TestName);
            throw;
        }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (_session is not null && TestContext is not null)
        {
            try
            {
                _session.CompleteTest(TestContext.TestName);
            }
            catch
            {
                // Outcome recorded by the test runner.
            }
        }

        _session?.Dispose();
        _session = null;
        EnsureNoAwayraProcesses(TimeSpan.FromSeconds(5));
    }

    private static void EnsureNoAwayraProcesses(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Awayra");
            if (processes.Length == 0)
            {
                return;
            }

            foreach (var process in processes)
            {
                try { process.Kill(true); } catch { }
            }

            Thread.Sleep(200);
        }
    }

    private static UiAutomationSession S => _session ?? throw new InvalidOperationException("Session not initialized.");

    [TestMethod]
    [Timeout(120_000)]
    public void UiTestProfile_LoadsOneMinuteEyeAndMoveIntervals()
    {
        Assert.AreEqual(1, S.EffectiveSettings.EyeResetIntervalMinutes);
        Assert.AreEqual(1, S.EffectiveSettings.MoveBreakIntervalMinutes);
        Assert.IsTrue(S.LatestDiagnostics.EyeRemainingSeconds is >= 50 and <= 70,
            $"Eye diagnostics={S.LatestDiagnostics.EyeRemainingSeconds}s");
        Assert.IsTrue(S.LatestDiagnostics.MoveRemainingSeconds is >= 50 and <= 70,
            $"Move diagnostics={S.LatestDiagnostics.MoveRemainingSeconds}s");

        var eye = S.FindElement("EyeCountdown")!.Name;
        var move = S.FindElement("MoveCountdown")!.Name;
        Assert.IsTrue(UiAutomationSession.ParseCountdownSeconds(eye) <= 70, $"Eye countdown={eye}");
        Assert.IsTrue(UiAutomationSession.ParseCountdownSeconds(move) <= 70, $"Move countdown={move}");
    }

    [TestMethod]
    [Timeout(120_000)]
    public void T17_ManualPauseResume_FreezesCountdowns()
    {
        var eyeBefore = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var moveBefore = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
        Assert.IsTrue(eyeBefore is >= 50 and <= 70, $"Unexpected Eye countdown start: {eyeBefore}s");
        Assert.IsTrue(moveBefore is >= 50 and <= 70, $"Unexpected Move countdown start: {moveBefore}s");

        S.FindElement("PauseResumeButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindElement("PauseResumeButton")!.Name.Contains("Resume", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10), "pause label");

        Thread.Sleep(3000);

        var eyePaused = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var movePaused = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
        Assert.IsTrue(Math.Abs(eyeBefore - eyePaused) <= 2, $"Eye moved from {eyeBefore} to {eyePaused} while paused");
        Assert.IsTrue(Math.Abs(moveBefore - movePaused) <= 2, $"Move moved from {moveBefore} to {movePaused} while paused");

        S.FindElement("PauseResumeButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindElement("PauseResumeButton")!.Name.Contains("Pause", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10), "resume label");

        var eyeResumed = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var moveResumed = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
        Assert.IsTrue(Math.Abs(eyePaused - eyeResumed) <= 2);
        Assert.IsTrue(Math.Abs(movePaused - moveResumed) <= 2);
    }

    [TestMethod]
    [Timeout(60_000)]
    public void T17b_SnoozeImmediatelyShowsPauseAndManualPauseFreezesSnooze()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        S.FindElement("EyeResetNowButton")!.AsButton().Invoke();
        UiPoll.Until(() => S.FindByAutomationId("EyeOverlayWindow"), TimeSpan.FromSeconds(10), "Eye overlay");
        S.FindElement("EyeSnoozeButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("EyeOverlayWindow") is null, TimeSpan.FromSeconds(10), "Eye overlay close");

        var afterSnooze = UiTestDiagnosticsClient.WaitUntil(
            d => d.Status == SchedulerStatus.Snoozed,
            TimeSpan.FromSeconds(10));
        Assert.AreEqual(SchedulerStatus.Snoozed, afterSnooze.Status);
        Assert.IsFalse(afterSnooze.IsPausedManual);

        UiPoll.UntilTrue(
            () => S.FindElement("PauseResumeButton")!.Name.Contains("Pause", StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(5),
            "pause label after eye snooze");

        var eyeSnoozed = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        S.FindElement("PauseResumeButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(
            () => S.FindElement("PauseResumeButton")!.Name.Contains("Resume", StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(5),
            "resume label after pause while snoozed");

        Thread.Sleep(2000);
        var eyePaused = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        Assert.IsTrue(Math.Abs(eyeSnoozed - eyePaused) <= 2,
            $"Eye snooze countdown should freeze on manual pause: {eyeSnoozed}s -> {eyePaused}s");

        S.FindElement("PauseResumeButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(
            () => S.FindElement("PauseResumeButton")!.Name.Contains("Pause", StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(5),
            "pause label after resume from snoozed pause");

        sw.Stop();
        Console.WriteLine($"[UiTest DURATION] T17b_SnoozeImmediatelyShowsPauseAndManualPauseFreezesSnooze={sw.Elapsed.TotalSeconds:F1}s");
    }

    [TestMethod]
    [Timeout(120_000)]
    public void T18_SimulatedIdle_ResetsFreshIntervals()
    {
        var before = UiTestDiagnosticsClient.ReadLatest();
        Assert.IsTrue(before.EyeRemainingSeconds is >= 50 and <= 70);

        S.SendUiTestCommand("SET_IDLE_TRUE");
        Thread.Sleep(3000);
        var idle = UiTestDiagnosticsClient.WaitUntil(d => d.IsIdlePaused, TimeSpan.FromSeconds(20));
        Assert.AreEqual(SchedulerStatus.Idle, idle.Status);

        Thread.Sleep(3000);
        var stillIdle = UiTestDiagnosticsClient.ReadLatest();
        Assert.IsTrue(stillIdle.IsIdlePaused);

        S.SendUiTestCommand("SET_IDLE_FALSE");
        var active = UiTestDiagnosticsClient.WaitUntil(d => !d.IsIdlePaused, TimeSpan.FromSeconds(15));
        Assert.AreEqual(SchedulerStatus.Running, active.Status);
        Assert.IsTrue(active.EyeRemainingSeconds is >= 50 and <= 70, $"Expected fresh ~60s eye interval, got {active.EyeRemainingSeconds}");
        Assert.IsTrue(active.MoveRemainingSeconds is >= 50 and <= 70, $"Expected fresh ~60s move interval, got {active.MoveRemainingSeconds}");
        S.CaptureScreenshot("after-idle-return.png");
    }

    [TestMethod]
    [Timeout(60_000)]
    public void T21_Idle_FreezesCountdownDisplays()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        UiPoll.UntilTrue(() =>
        {
            var eye = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
            var move = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
            return eye < 60 && move < 60;
        }, TimeSpan.FromSeconds(15), "countdowns below one minute");

        Thread.Sleep(5000);

        var eyeBeforeIdle = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var moveBeforeIdle = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
        Assert.IsTrue(eyeBeforeIdle < 60, $"Eye should be below 60s before idle, got {eyeBeforeIdle}");
        Assert.IsTrue(moveBeforeIdle < 60, $"Move should be below 60s before idle, got {moveBeforeIdle}");

        S.SendUiTestCommand("SET_IDLE_TRUE");
        var idle = UiTestDiagnosticsClient.WaitUntil(d => d.IsIdlePaused, TimeSpan.FromSeconds(10));
        Assert.AreEqual(SchedulerStatus.Idle, idle.Status);

        var eyeFrozen = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var moveFrozen = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);

        Thread.Sleep(9000);

        var eyeAfterWait = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var moveAfterWait = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
        Assert.IsTrue(Math.Abs(eyeFrozen - eyeAfterWait) <= 1,
            $"Eye countdown drifted during idle: {eyeFrozen}s -> {eyeAfterWait}s");
        Assert.IsTrue(Math.Abs(moveFrozen - moveAfterWait) <= 1,
            $"Move countdown drifted during idle: {moveFrozen}s -> {moveAfterWait}s");

        S.SendUiTestCommand("SET_IDLE_FALSE");
        var active = UiTestDiagnosticsClient.WaitUntil(d => !d.IsIdlePaused, TimeSpan.FromSeconds(10));
        Assert.AreEqual(SchedulerStatus.Running, active.Status);

        var eyeAfterReturn = UiAutomationSession.ParseCountdownSeconds(S.FindElement("EyeCountdown")!.Name);
        var moveAfterReturn = UiAutomationSession.ParseCountdownSeconds(S.FindElement("MoveCountdown")!.Name);
        Assert.IsTrue(eyeAfterReturn is >= 55 and <= 65,
            $"Expected Eye ~60s after idle return, got {eyeAfterReturn}s");
        Assert.IsTrue(moveAfterReturn is >= 55 and <= 65,
            $"Expected Move ~60s after idle return, got {moveAfterReturn}s");

        sw.Stop();
        Console.WriteLine($"[UiTest DURATION] T21_Idle_FreezesCountdownDisplays={sw.Elapsed.TotalSeconds:F1}s");
        S.CaptureScreenshot("after-idle-freeze-return.png");
    }

    [TestMethod]
    [Timeout(120_000)]
    public void T19_SettingsPause_SaveResetsAndCancelResumes()
    {
        var before = UiTestDiagnosticsClient.ReadLatest();
        Assert.IsTrue(before.EyeRemainingSeconds is >= 50 and <= 70);

        S.FindElement("SettingsButton")!.AsButton().Invoke();
        var settingsWindow = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings")!;
        S.CaptureScreenshot("settings-paused.png");

        var pausedDiag = UiTestDiagnosticsClient.WaitUntil(d => d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        var frozenEye = pausedDiag.EyeRemainingSeconds;

        Thread.Sleep(3000);

        var stillPaused = UiTestDiagnosticsClient.ReadLatest();
        Assert.IsTrue(stillPaused.IsConfigurationPaused);
        Assert.AreEqual(frozenEye, stillPaused.EyeRemainingSeconds, 1);

        S.FindElement("SettingsCloseButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings close");

        var afterCancel = UiTestDiagnosticsClient.WaitUntil(d => !d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        Assert.IsTrue(afterCancel.EyeRemainingSeconds <= frozenEye);
        Assert.IsTrue(afterCancel.EyeRemainingSeconds >= frozenEye - 8,
            $"Cancel should resume frozen Eye countdown: frozen={frozenEye}, actual={afterCancel.EyeRemainingSeconds}");

        S.FindElement("SettingsButton")!.AsButton().Invoke();
        settingsWindow = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings reopen")!;
        var intervalBoxes = settingsWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.IsTrue(intervalBoxes.Length > 0);
        intervalBoxes[0].AsTextBox().Text = "2";

        S.FindElement("SettingsSaveButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings save");

        var afterSave = UiTestDiagnosticsClient.WaitUntil(d => !d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        Assert.IsTrue(afterSave.EyeRemainingSeconds is >= 115 and <= 125,
            $"Expected ~120s after Eye interval save, got {afterSave.EyeRemainingSeconds}");
        S.CaptureScreenshot("after-settings-save.png");
    }

    [TestMethod]
    [Timeout(90_000)]
    public void T22_SettingsSaveScheduleTransitions()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var baseline = UiTestDiagnosticsClient.ReadLatest();
        Assert.IsTrue(baseline.EyeRemainingSeconds is >= 50 and <= 70);
        Assert.IsTrue(baseline.MoveRemainingSeconds is >= 50 and <= 70);

        S.FindElement("SettingsButton")!.AsButton().Invoke();
        var settingsWindow = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings")!;
        var pausedDiag = UiTestDiagnosticsClient.WaitUntil(d => d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        var frozenEye = pausedDiag.EyeRemainingSeconds;
        var frozenMove = pausedDiag.MoveRemainingSeconds;

        Thread.Sleep(3000);
        S.FindElement("SettingsSaveButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings save no changes");

        var afterNoChanges = UiTestDiagnosticsClient.WaitUntil(d => !d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        Assert.IsTrue(afterNoChanges.EyeRemainingSeconds <= frozenEye + 1,
            $"Eye should resume frozen value: frozen={frozenEye}, actual={afterNoChanges.EyeRemainingSeconds}");
        Assert.IsTrue(afterNoChanges.EyeRemainingSeconds >= frozenEye - 10,
            $"Eye should resume frozen value: frozen={frozenEye}, actual={afterNoChanges.EyeRemainingSeconds}");
        Assert.IsTrue(afterNoChanges.MoveRemainingSeconds <= frozenMove + 1,
            $"Move should resume frozen value: frozen={frozenMove}, actual={afterNoChanges.MoveRemainingSeconds}");
        Assert.IsTrue(afterNoChanges.MoveRemainingSeconds >= frozenMove - 10,
            $"Move should resume frozen value: frozen={frozenMove}, actual={afterNoChanges.MoveRemainingSeconds}");

        S.FindElement("SettingsButton")!.AsButton().Invoke();
        settingsWindow = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings eye change")!;
        var moveBeforeEyeChange = UiTestDiagnosticsClient.WaitUntil(d => d.IsConfigurationPaused, TimeSpan.FromSeconds(10)).MoveRemainingSeconds;
        var intervalBoxes = settingsWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        intervalBoxes[0].AsTextBox().Text = "2";
        S.FindElement("SettingsSaveButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings save eye change");

        var afterEyeChange = UiTestDiagnosticsClient.WaitUntil(d => !d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        Assert.IsTrue(afterEyeChange.EyeRemainingSeconds is >= 115 and <= 125,
            $"Eye-only change should reset Eye to ~120s, got {afterEyeChange.EyeRemainingSeconds}");
        Assert.IsTrue(afterEyeChange.MoveRemainingSeconds <= moveBeforeEyeChange + 1,
            $"Move should not reset on Eye-only save: before={moveBeforeEyeChange}, after={afterEyeChange.MoveRemainingSeconds}");
        Assert.IsTrue(afterEyeChange.MoveRemainingSeconds >= moveBeforeEyeChange - 10,
            $"Move should resume near frozen value on Eye-only save: before={moveBeforeEyeChange}, after={afterEyeChange.MoveRemainingSeconds}");

        S.FindElement("SettingsButton")!.AsButton().Invoke();
        settingsWindow = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings move change")!;
        var eyeBeforeMoveChange = UiTestDiagnosticsClient.WaitUntil(d => d.IsConfigurationPaused, TimeSpan.FromSeconds(10)).EyeRemainingSeconds;
        intervalBoxes = settingsWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        intervalBoxes[2].AsTextBox().Text = "2";
        S.FindElement("SettingsSaveButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings save move change");

        var afterMoveChange = UiTestDiagnosticsClient.WaitUntil(d => !d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        Assert.IsTrue(afterMoveChange.MoveRemainingSeconds is >= 115 and <= 125,
            $"Move-only change should reset Move to ~120s, got {afterMoveChange.MoveRemainingSeconds}");
        Assert.IsTrue(afterMoveChange.EyeRemainingSeconds <= eyeBeforeMoveChange + 1,
            $"Eye should not reset on Move-only save: before={eyeBeforeMoveChange}, after={afterMoveChange.EyeRemainingSeconds}");
        Assert.IsTrue(afterMoveChange.EyeRemainingSeconds >= eyeBeforeMoveChange - 10,
            $"Eye should resume near frozen value on Move-only save: before={eyeBeforeMoveChange}, after={afterMoveChange.EyeRemainingSeconds}");

        S.FindElement("SettingsButton")!.AsButton().Invoke();
        settingsWindow = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings glass change")!;
        var eyeBeforeGlass = UiTestDiagnosticsClient.WaitUntil(d => d.IsConfigurationPaused, TimeSpan.FromSeconds(10)).EyeRemainingSeconds;
        var moveBeforeGlass = UiTestDiagnosticsClient.ReadLatest().MoveRemainingSeconds;
        var slider = settingsWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Slider))!.AsSlider();
        slider.Value = slider.Value >= 100 ? 50 : 100;
        S.FindElement("SettingsSaveButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings save glass change");

        var afterGlassChange = UiTestDiagnosticsClient.WaitUntil(d => !d.IsConfigurationPaused, TimeSpan.FromSeconds(10));
        Assert.IsTrue(afterGlassChange.EyeRemainingSeconds <= eyeBeforeGlass + 1,
            $"Glass-only save should preserve Eye: before={eyeBeforeGlass}, after={afterGlassChange.EyeRemainingSeconds}");
        Assert.IsTrue(afterGlassChange.EyeRemainingSeconds >= eyeBeforeGlass - 10,
            $"Glass-only save should preserve Eye: before={eyeBeforeGlass}, after={afterGlassChange.EyeRemainingSeconds}");
        Assert.IsTrue(afterGlassChange.MoveRemainingSeconds <= moveBeforeGlass + 1,
            $"Glass-only save should preserve Move: before={moveBeforeGlass}, after={afterGlassChange.MoveRemainingSeconds}");
        Assert.IsTrue(afterGlassChange.MoveRemainingSeconds >= moveBeforeGlass - 10,
            $"Glass-only save should preserve Move: before={moveBeforeGlass}, after={afterGlassChange.MoveRemainingSeconds}");

        sw.Stop();
        Console.WriteLine($"[UiTest DURATION] T22_SettingsSaveScheduleTransitions={sw.Elapsed.TotalSeconds:F1}s");
        S.CaptureScreenshot("after-settings-save-transitions.png");
    }

    [TestMethod]
    public void T20_RealWindowsIdle_IsOptionalSmoke()
    {
        try
        {
            var idleReady = UiTestDiagnosticsClient.WaitUntil(d => d.IdleSeconds >= 65, TimeSpan.FromSeconds(30));
            Assert.IsTrue(idleReady.IsIdlePaused);
        }
        catch (TimeoutException)
        {
            Assert.Inconclusive("System idle did not reach 65 seconds in this environment.");
        }
    }
}
