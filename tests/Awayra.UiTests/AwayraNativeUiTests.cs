using System.Diagnostics;
using System.Text.RegularExpressions;
using Awayra.Core.Models;
using Awayra.UiTests.Support;
using FlaUI.Core.AutomationElements;

namespace Awayra.UiTests;

[TestClass]
public sealed class AwayraNativeUiTests
{
    private static UiAutomationSession? _session;
    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        foreach (var process in Process.GetProcessesByName("Awayra"))
        {
            try { process.Kill(true); } catch { }
        }
    }

    [TestInitialize]
    public void TestInit()
    {
        foreach (var process in Process.GetProcessesByName("Awayra"))
        {
            try { process.Kill(true); } catch { }
        }

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
        foreach (var process in Process.GetProcessesByName("Awayra"))
        {
            try { process.Kill(true); } catch { }
        }
    }

    private static UiAutomationSession S => _session ?? throw new InvalidOperationException("Session not initialized.");

    [TestMethod]
    public void T01_CleanFirstLaunch_ShowsDashboard()
    {
        var main = S.MainWindow;
        Assert.IsTrue(main.IsAvailable);
        Assert.IsFalse(main.IsOffscreen);
        Assert.IsNotNull(S.FindElement("DashboardAboutSupportButton"));
        S.CaptureScreenshot("dashboard-about-support.png");
    }

    [TestMethod]
    public void T02_StartMinimized_DefaultsFalse()
    {
        var log = S.ReadLogTail();
        Assert.IsTrue(log.Contains("Awayra started.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void T03_ProcessPathMatchesTestedExecutable()
    {
        var proc = Process.GetProcessesByName("Awayra").Single();
        Assert.AreEqual(S.ExecutablePath, proc.MainModule!.FileName, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void T04_ProcessHashMatchesTestedBuild()
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(S.ExecutablePath)));
        Assert.AreEqual(S.ExecutableSha256, hash);
    }

    [TestMethod]
    public void T05_LogRecordsBuildIdentity()
    {
        var log = S.ReadLogTail();
        StringAssert.Contains(log, "BuildIdentity AssemblyVersion=");
        StringAssert.Contains(log, "BuildIdentity ExecutablePath=");
        StringAssert.Contains(log, "BuildIdentity ExecutableSha256=");
        StringAssert.Contains(log, "BuildIdentity ProcessId=");
    }

    [TestMethod]
    public void T06_SingleOldProcessGuard()
    {
        Assert.AreEqual(1, Process.GetProcessesByName("Awayra").Length);
    }

    [TestMethod]
    public void T07_EyeCountdownVisibleAndChanges()
    {
        var eye = S.FindElement("EyeCountdown") ?? throw new AssertFailedException("EyeCountdown missing");
        var first = eye.Name;
        Thread.Sleep(1500);
        var second = S.FindElement("EyeCountdown")!.Name;
        Assert.IsFalse(string.IsNullOrWhiteSpace(first));
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void T08_MoveCountdownVisibleAndChanges()
    {
        var move = S.FindElement("MoveCountdown") ?? throw new AssertFailedException("MoveCountdown missing");
        var first = move.Name;
        Thread.Sleep(1500);
        var second = S.FindElement("MoveCountdown")!.Name;
        Assert.IsFalse(string.IsNullOrWhiteSpace(first));
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void T09_SettingsButtonOpensSettings()
    {
        S.FindElement("SettingsButton")!.AsButton().Invoke();
        var settings = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "SettingsWindow");
        Assert.IsNotNull(settings);
        Assert.IsNull(S.FindByAutomationId("AboutAwayraButton"));
        S.CaptureScreenshot("settings-no-scroll-980x720.png");
    }

    [TestMethod]
    public void T09b_AboutOpensAndClosesFromDashboard()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var before = UiTestDiagnosticsClient.WaitUntil(d => d.EyeRemainingSeconds > 0, TimeSpan.FromSeconds(10));
        var frozenEye = before.EyeRemainingSeconds;
        var frozenMove = before.MoveRemainingSeconds;
        var frozenSnooze = before.Status;
        var frozenPause = before.IsPausedManual;

        S.FindElement("DashboardAboutSupportButton")!.AsButton().Invoke();
        var about = UiPoll.Until(() => S.FindByAutomationId("AboutWindow"), TimeSpan.FromSeconds(10), "AboutWindow");
        Assert.IsNotNull(about);
        S.CaptureScreenshot("about-window-from-dashboard.png");

        var aboutText = about.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        var combined = string.Join(' ', aboutText.Select(t => t.Name));
        StringAssert.Contains(combined, "Version");
        StringAssert.Contains(combined, "Farzin Alavi");
        StringAssert.Contains(combined, "Your work matters");
        StringAssert.Contains(combined, "Support link is not configured yet.");

        var supportButton = S.FindElement("AboutSupportButton")!.AsButton();
        Assert.IsFalse(supportButton.IsEnabled);

        S.FindElement("AboutCloseButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("AboutWindow") is null, TimeSpan.FromSeconds(10), "About close");

        var after = UiTestDiagnosticsClient.ReadLatest();
        var elapsedSeconds = (int)Math.Ceiling(sw.Elapsed.TotalSeconds);
        Assert.IsTrue(Math.Abs(frozenEye - after.EyeRemainingSeconds) <= elapsedSeconds + 2,
            $"Eye remaining changed unexpectedly: {frozenEye}s -> {after.EyeRemainingSeconds}s over {elapsedSeconds}s");
        Assert.IsTrue(Math.Abs(frozenMove - after.MoveRemainingSeconds) <= elapsedSeconds + 2,
            $"Move remaining changed unexpectedly: {frozenMove}s -> {after.MoveRemainingSeconds}s over {elapsedSeconds}s");
        Assert.AreEqual(frozenSnooze, after.Status);
        Assert.AreEqual(frozenPause, after.IsPausedManual);
        Assert.IsFalse(after.IsConfigurationPaused);

        sw.Stop();
        Console.WriteLine($"[UiTest DURATION] T09b_AboutOpensAndClosesFromDashboard={sw.Elapsed.TotalSeconds:F1}s");
    }

    [TestMethod]
    public void T10_SingleInstanceSecondLaunchRestoresDashboard()
    {
        S.MainWindow.Close();
        Thread.Sleep(500);
        S.LaunchSecondInstance();
        UiPoll.UntilTrue(() => S.FindByAutomationId("MainWindow")?.IsOffscreen == false, TimeSpan.FromSeconds(10), "Dashboard restore");
        Assert.AreEqual(1, Process.GetProcessesByName("Awayra").Length);
        Assert.AreEqual(S.ExecutablePath, Process.GetProcessesByName("Awayra").Single().MainModule!.FileName, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void T11_TrayOpenCoordination()
    {
        S.MainWindow.Close();
        S.SendUiTestCommand("TRAY_OPEN");
        UiPoll.UntilTrue(() => S.FindByAutomationId("MainWindow")?.IsOffscreen == false, TimeSpan.FromSeconds(10), "Tray open");
    }

    [TestMethod]
    public void T12_TraySettingsCoordination()
    {
        S.SendUiTestCommand("TRAY_SETTINGS");
        Assert.IsNotNull(UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "Tray settings"));
    }

    [TestMethod]
    public void T13_EyeOverlayOpensAndSnoozes()
    {
        S.FindElement("EyeResetNowButton")!.AsButton().Invoke();
        var overlay = UiPoll.Until(() => S.FindByAutomationId("EyeOverlayWindow"), TimeSpan.FromSeconds(10), "Eye overlay");
        Assert.IsNotNull(overlay);
        Assert.IsNotNull(S.FindElement("EyeOverlayCountdown"));
        S.FindElement("EyeSnoozeButton")!.AsButton().Invoke();
        UiPoll.UntilTrue(() => S.FindByAutomationId("EyeOverlayWindow") is null, TimeSpan.FromSeconds(10), "Eye overlay close");
        var eye = S.FindElement("EyeCountdown")!.Name;
        Assert.IsTrue(Regex.IsMatch(eye, @"^00:5[89]|^01:00"));
        S.CaptureScreenshot("eye-after-snooze.png");
    }

    [TestMethod]
    [Timeout(60_000)]
    public void T14_MoveOverlayOpensAndSnoozes()
    {
        var sw = Stopwatch.StartNew();

        var before = UiTestDiagnosticsClient.ReadLatest();
        var snoozedBefore = before.Snoozed;

        Assert.AreEqual(1, S.EffectiveSettings.MoveBreakIntervalMinutes);
        Assert.AreEqual(1, S.EffectiveSettings.EyeResetIntervalMinutes);
        Assert.IsTrue(before.MoveRemainingSeconds is >= 50 and <= 70,
            $"Expected 1-minute Move profile, got {before.MoveRemainingSeconds}s remaining");

        S.FindElement("MoveBreakNowButton")!.AsButton().Invoke();
        var overlay = UiPoll.Until(() => S.FindByAutomationId("MoveOverlayWindow"), TimeSpan.FromSeconds(10), "Move overlay");
        Assert.IsNotNull(overlay);
        S.CaptureScreenshot("move-overlay-100.png");

        var eyeBaseline = UiTestDiagnosticsClient.ReadLatest();
        var eyeRemainingBefore = eyeBaseline.EyeRemainingSeconds;
        var eyeNextDueBefore = eyeBaseline.EyeNextDue;
        var eyeSnoozeUntilBefore = eyeBaseline.EyeSnoozeUntil;

        var snoozeAt = DateTimeOffset.Now;
        S.FindElement("MoveSnoozeButton")!.AsButton().Invoke();

        UiPoll.UntilTrue(() => S.FindByAutomationId("MoveOverlayWindow") is null, TimeSpan.FromSeconds(10), "Move overlay close");

        var main = S.MainWindow;
        Assert.IsTrue(main.IsAvailable);
        Assert.IsFalse(main.IsOffscreen);

        var after = UiTestDiagnosticsClient.WaitUntil(
            d => d.Status == SchedulerStatus.Snoozed && d.MoveRemainingSeconds is >= 55 and <= 60,
            TimeSpan.FromSeconds(10));

        Assert.AreEqual(SchedulerStatus.Snoozed, after.Status);
        Assert.IsTrue(after.MoveRemainingSeconds is >= 55 and <= 60,
            $"MoveRemainingSeconds={after.MoveRemainingSeconds}");
        Assert.IsNotNull(after.MoveSnoozeUntil);

        var moveNextDueDelta = (after.MoveNextDue - snoozeAt).TotalSeconds;
        Assert.IsTrue(moveNextDueDelta is >= 55 and <= 65,
            $"MoveNextDue should be ~1 minute after snooze, delta={moveNextDueDelta:F1}s");

        var moveCountdown = S.FindElement("MoveCountdown")!.Name;
        var moveSeconds = UiAutomationSession.ParseCountdownSeconds(moveCountdown);
        Assert.IsTrue(moveSeconds is >= 55 and <= 60, $"Move countdown={moveCountdown}");

        Assert.AreEqual(snoozedBefore + 1, after.Snoozed,
            $"Snoozed count should increment once: before={snoozedBefore}, after={after.Snoozed}");

        Assert.IsTrue(Math.Abs(after.EyeRemainingSeconds - eyeRemainingBefore) <= 2,
            $"Eye remaining changed unexpectedly: {eyeRemainingBefore}s -> {after.EyeRemainingSeconds}s");
        Assert.AreEqual(eyeSnoozeUntilBefore, after.EyeSnoozeUntil,
            "Eye snooze state should remain unchanged after Move snooze");
        Assert.IsTrue(Math.Abs((after.EyeNextDue - eyeNextDueBefore).TotalSeconds) <= 2,
            $"Eye next due changed unexpectedly: {eyeNextDueBefore:o} -> {after.EyeNextDue:o}");

        Assert.IsNull(S.FindByAutomationId("MoveOverlayWindow"));
        Assert.IsNull(S.FindByAutomationId("EyeOverlayWindow"));

        var settings = S.LoadEffectiveSettings();
        Assert.AreEqual(1, settings.MoveBreakIntervalMinutes);
        Assert.AreEqual(1, settings.EyeResetIntervalMinutes);
        Assert.IsTrue(after.MoveRemainingSeconds <= 70, "Move countdown must not reflect production intervals");
        Assert.IsTrue(after.EyeRemainingSeconds <= 70, "Eye countdown must not reflect production intervals");

        sw.Stop();
        Console.WriteLine($"[UiTest DURATION] T14_MoveOverlayOpensAndSnoozes={sw.Elapsed.TotalSeconds:F1}s");
        S.CaptureScreenshot("move-after-snooze.png");
    }

    [TestMethod]
    public void T15_SettingsGlassClarityAndNoLanguage()
    {
        S.FindElement("SettingsButton")!.AsButton().Invoke();
        var settings = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "SettingsWindow");
        Assert.IsNotNull(settings);
        var slider = settings!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Slider));
        Assert.IsNotNull(slider, "Glass clarity slider not found in Settings window.");
        Assert.IsNull(S.FindByAutomationId("LanguageSelector"));
        S.FindElement("SettingsCloseButton")!.AsButton().Invoke();
    }

    [TestMethod]
    public void T16_GlassClarityScreenshotsDiffer()
    {
        foreach (var level in new[] { 0, 50, 100, 125, 150 })
        {
            S.SendUiTestCommand("TRAY_SETTINGS");
            var settings = UiPoll.Until(() => S.FindByAutomationId("SettingsWindow"), TimeSpan.FromSeconds(10), "settings")!;
            var slider = settings.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Slider))!.AsSlider();
            slider.Value = level;
            S.FindElement("SettingsSaveButton")!.AsButton().Invoke();
            UiPoll.UntilTrue(() => S.FindByAutomationId("SettingsWindow") is null, TimeSpan.FromSeconds(10), "settings close");
            Thread.Sleep(500);
            S.FindElement("EyeResetNowButton")!.AsButton().Invoke();
            UiPoll.Until(() => S.FindByAutomationId("EyeOverlayWindow"), TimeSpan.FromSeconds(10), "overlay");
            S.CaptureScreenshot($"glass-clarity-{level}.png");
            S.FindElement("EyeCompleteButton")!.AsButton().Invoke();
            UiPoll.UntilTrue(() => S.FindByAutomationId("EyeOverlayWindow") is null, TimeSpan.FromSeconds(10), "overlay close");
        }

        var files = new[] { 0, 50, 100, 125, 150 }.Select(l => File.ReadAllBytes(Path.Combine(S.ScreenshotDir, $"glass-clarity-{l}.png"))).ToArray();
        for (var i = 1; i < files.Length; i++)
        {
            Assert.IsFalse(files[i - 1].SequenceEqual(files[i]), $"glass-clarity-{new[] { 0, 50, 100, 125, 150 }[i - 1]} and glass-clarity-{new[] { 0, 50, 100, 125, 150 }[i]} should differ");
        }
    }
}
