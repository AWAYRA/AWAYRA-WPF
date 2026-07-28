using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Capture = FlaUI.Core.Capturing.Capture;

namespace Awayra.UiTests.Support;

public sealed class UiAutomationSession : IDisposable
{
    private const int MaxCountdownSeconds = 70;
    private readonly string _repoRoot;
    private readonly string _screenshotDir;
    private Application? _app;
    private UIA3Automation? _automation;
    private Process? _process;
    private string? _currentTestName;

    public UiAutomationSession()
    {
        _repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _screenshotDir = Path.Combine(_repoRoot, "artifacts", "ui-audit");
        Directory.CreateDirectory(_screenshotDir);
    }

    public string ExecutablePath { get; private set; } = string.Empty;
    public string ExecutableSha256 { get; private set; } = string.Empty;
    public string ScreenshotDir => _screenshotDir;
    public string? DataRoot { get; private set; }
    public AppSettings EffectiveSettings { get; private set; } = AppSettings.CreateDefault();
    public SchedulerDiagnostics LatestDiagnostics { get; private set; } = new();

    public void BeginTest(string testName)
    {
        _currentTestName = testName;
        Console.WriteLine($"[UiTest START] {testName}");
    }

    public void CompleteTest(string testName)
    {
        Console.WriteLine($"[UiTest PASS] {testName}");
    }

    public void Launch(string configuration = "Debug")
    {
        DataRoot = Path.Combine(Path.GetTempPath(), $"Awayra-UiTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(Path.Combine(DataRoot, "Logs"));

        WriteHarnessSettings(DataRoot);
        WriteHarnessState(DataRoot);

        var overrideExe = Environment.GetEnvironmentVariable("AWAYRA_UI_TEST_EXE");
        if (!string.IsNullOrWhiteSpace(overrideExe))
        {
            ExecutablePath = Path.GetFullPath(overrideExe);
        }
        else
        {
            var candidates = new[]
            {
                Path.Combine(_repoRoot, "src", "Awayra.App", "bin", configuration, "net10.0-windows", "Awayra.exe"),
                Path.Combine(_repoRoot, "src", "Awayra.App", "bin", configuration, "net10.0-windows", "win-x64", "Awayra.exe")
            };
            ExecutablePath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        if (!File.Exists(ExecutablePath))
        {
            throw new FileNotFoundException("Awayra executable not found.", ExecutablePath);
        }

        ExecutableSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExecutablePath)));
        _automation = new UIA3Automation();
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = $"--ui-test --ui-test-data-root \"{DataRoot}\"",
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath) ?? _repoRoot
        };
        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to launch {ExecutablePath}");
        _app = Application.Attach(_process);

        UiTestDiagnosticsClient.UseDataRoot(DataRoot);
        WaitForDiagnosticsFile(TimeSpan.FromSeconds(15));
        SendUiTestCommand("SET_IDLE_FALSE");
        UiTestDiagnosticsClient.WaitUntil(d => !d.IsIdlePaused, TimeSpan.FromSeconds(10));
        VerifyUiTestProfileLoaded(TimeSpan.FromSeconds(10));
        WaitForMainWindow(TimeSpan.FromSeconds(20));
    }

    public static AppSettings CreateHarnessSettings() => new()
    {
        SchemaVersion = AppSettings.CurrentSchemaVersion,
        EyeResetEnabled = true,
        EyeResetIntervalMinutes = 1,
        EyeResetDurationSeconds = 10,
        MoveBreakEnabled = true,
        MoveBreakIntervalMinutes = 1,
        MoveBreakDurationSeconds = 10,
        SnoozeDurationMinutes = 1,
        PauseWhileIdle = true,
        IdleThresholdMinutes = 1,
        WorkHoursEnabled = false,
        StartMinimized = false,
        RunAtStartup = false,
        AllowSnooze = true,
        AllowSkip = true
    };

    private static void WriteHarnessSettings(string dataRoot)
    {
        var settings = CreateHarnessSettings();
        var json = JsonSerializer.Serialize(settings, JsonOptions.Create());
        File.WriteAllText(Path.Combine(dataRoot, "settings.json"), json);
    }

    private static void WriteHarnessState(string dataRoot)
    {
        var now = DateTimeOffset.Now;
        var state = new SchedulerState
        {
            SchemaVersion = SchedulerState.CurrentSchemaVersion,
            EyeNextDue = now.AddMinutes(1),
            MoveNextDue = now.AddMinutes(1),
            LastClockCheck = now
        };
        var json = JsonSerializer.Serialize(state, JsonOptions.Create());
        File.WriteAllText(Path.Combine(dataRoot, "state.json"), json);
    }

    private void WaitForDiagnosticsFile(TimeSpan timeout)
    {
        var path = Path.Combine(DataRoot!, "diagnostics.json");
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 2)
            {
                return;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"diagnostics.json was not created under {DataRoot}. Log tail:{Environment.NewLine}{ReadLogTail()}");
    }

    public void VerifyUiTestProfileLoaded(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                EffectiveSettings = LoadEffectiveSettings();
                LatestDiagnostics = UiTestDiagnosticsClient.ReadLatest();

                if (EffectiveSettings.EyeResetIntervalMinutes != 1 ||
                    EffectiveSettings.MoveBreakIntervalMinutes != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected 1-minute Eye/Move intervals, got Eye={EffectiveSettings.EyeResetIntervalMinutes}, Move={EffectiveSettings.MoveBreakIntervalMinutes}.");
                }

                if (LatestDiagnostics.EyeRemainingSeconds <= 0 ||
                    LatestDiagnostics.MoveRemainingSeconds <= 0 ||
                    LatestDiagnostics.EyeRemainingSeconds > MaxCountdownSeconds ||
                    LatestDiagnostics.MoveRemainingSeconds > MaxCountdownSeconds)
                {
                    throw new InvalidOperationException(
                        $"Diagnostics countdown out of range: Eye={LatestDiagnostics.EyeRemainingSeconds}s Move={LatestDiagnostics.MoveRemainingSeconds}s.");
                }

                var eyeCountdown = FindElement("EyeCountdown", TimeSpan.FromSeconds(2))?.Name;
                var moveCountdown = FindElement("MoveCountdown", TimeSpan.FromSeconds(2))?.Name;
                if (!string.IsNullOrWhiteSpace(eyeCountdown) && !string.IsNullOrWhiteSpace(moveCountdown))
                {
                    var eyeSeconds = ParseCountdownSeconds(eyeCountdown);
                    var moveSeconds = ParseCountdownSeconds(moveCountdown);
                    if (eyeSeconds > MaxCountdownSeconds || moveSeconds > MaxCountdownSeconds)
                    {
                        throw new InvalidOperationException(
                            $"Visible countdown above 01:10: Eye={eyeCountdown}, Move={moveCountdown}.");
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Thread.Sleep(200);
            }
        }

        SaveFailureArtifacts(_currentTestName ?? "VerifyUiTestProfileLoaded");
        throw new TimeoutException($"UI-test profile verification failed: {lastError?.Message}", lastError);
    }

    public AppSettings LoadEffectiveSettings()
    {
        var settingsPath = Path.Combine(DataRoot!, "settings.json");
        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException("settings.json missing from UI-test data root.", settingsPath);
        }

        return SettingsRecovery.LoadWithRecovery(File.ReadAllText(settingsPath));
    }

    public Window MainWindow => WaitForMainWindow(TimeSpan.FromSeconds(5));

    public Window WaitForMainWindow(TimeSpan timeout)
    {
        if (_automation is null || _app is null)
        {
            throw new InvalidOperationException("Session not launched.");
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var window = FindByAutomationId("MainWindow");
            if (window is not null && !window.IsOffscreen)
            {
                return window;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("MainWindow was not found.");
    }

    public Window? FindByAutomationId(string automationId)
    {
        if (_automation is null)
        {
            return null;
        }

        var desktop = _automation.GetDesktop();
        var condition = _automation.ConditionFactory.ByAutomationId(automationId);
        return desktop.FindFirstDescendant(condition)?.AsWindow();
    }

    public AutomationElement? FindElement(string automationId, TimeSpan? timeout = null)
    {
        if (_automation is null)
        {
            return null;
        }

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var condition = _automation.ConditionFactory.ByAutomationId(automationId);
        while (DateTime.UtcNow < deadline)
        {
            var desktop = _automation.GetDesktop();
            var element = desktop.FindFirstDescendant(condition);
            if (element is not null)
            {
                return element;
            }

            foreach (var window in desktop.FindAllChildren(_automation.ConditionFactory.ByControlType(ControlType.Window)))
            {
                element = window.FindFirstDescendant(condition);
                if (element is not null)
                {
                    return element;
                }
            }

            try
            {
                element = MainWindow.FindFirstDescendant(condition);
                if (element is not null)
                {
                    return element;
                }
            }
            catch
            {
                // Main window may not be ready yet.
            }

            Thread.Sleep(200);
        }

        return null;
    }

    public string CaptureScreenshot(string fileName)
    {
        var path = Path.Combine(_screenshotDir, fileName);
        Capture.Element(MainWindow).ToFile(path);
        return path;
    }

    public void SendUiTestCommand(string command)
    {
        using var client = new System.IO.Pipes.NamedPipeClientStream(".", "Awayra.UiTest.Commands", System.IO.Pipes.PipeDirection.Out);
        client.Connect(5000);
        using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
        writer.WriteLine(command);
        Thread.Sleep(300);
    }

    public void LaunchSecondInstance()
    {
        Process.Start(new ProcessStartInfo(ExecutablePath) { UseShellExecute = true });
        Thread.Sleep(1500);
    }

    public string ReadLogTail(int maxLines = 40)
    {
        if (DataRoot is null)
        {
            return string.Empty;
        }

        var logPath = Path.Combine(DataRoot, "Logs", "awayra.log");
        if (!File.Exists(logPath))
        {
            return string.Empty;
        }

        var lines = File.ReadAllLines(logPath);
        return string.Join(Environment.NewLine, lines.TakeLast(maxLines));
    }

    public void SaveFailureArtifacts(string testName)
    {
        var safeName = Regex.Replace(testName, @"[^\w\-]+", "_");
        try
        {
            CaptureScreenshot($"FAIL-{safeName}.png");
        }
        catch
        {
            // Best effort only.
        }

        if (DataRoot is null)
        {
            return;
        }

        var artifactDir = Path.Combine(_screenshotDir, "failures", safeName);
        Directory.CreateDirectory(artifactDir);

        CopyIfExists(Path.Combine(DataRoot, "settings.json"), Path.Combine(artifactDir, "settings.json"));
        CopyIfExists(Path.Combine(DataRoot, "diagnostics.json"), Path.Combine(artifactDir, "diagnostics.json"));
        CopyIfExists(Path.Combine(DataRoot, "Logs", "awayra.log"), Path.Combine(artifactDir, "awayra.log"));

        File.WriteAllText(
            Path.Combine(artifactDir, "process.txt"),
            $"ExecutablePath={ExecutablePath}{Environment.NewLine}ProcessId={_process?.Id}{Environment.NewLine}DataRoot={DataRoot}");

        try
        {
            LatestDiagnostics = UiTestDiagnosticsClient.ReadLatest();
            EffectiveSettings = LoadEffectiveSettings();
            File.WriteAllText(
                Path.Combine(artifactDir, "summary.txt"),
                $"EyeIntervalMinutes={EffectiveSettings.EyeResetIntervalMinutes}{Environment.NewLine}" +
                $"MoveIntervalMinutes={EffectiveSettings.MoveBreakIntervalMinutes}{Environment.NewLine}" +
                $"EyeRemainingSeconds={LatestDiagnostics.EyeRemainingSeconds}{Environment.NewLine}" +
                $"MoveRemainingSeconds={LatestDiagnostics.MoveRemainingSeconds}{Environment.NewLine}" +
                $"LogTail:{Environment.NewLine}{ReadLogTail()}");
        }
        catch
        {
            // Best effort only.
        }
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Copy(source, destination, overwrite: true);
        }
    }

    public static int ParseCountdownSeconds(string value)
    {
        var parts = value.Split(':');
        return parts.Length switch
        {
            3 => int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2]),
            2 => int.Parse(parts[0]) * 60 + int.Parse(parts[1]),
            _ => int.Parse(value)
        };
    }

    public void Dispose()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                try { SendUiTestCommand("QUIT"); Thread.Sleep(1000); } catch { }
                if (!_process.HasExited) { _process.Kill(true); }
            }
        }
        catch { }

        _automation?.Dispose();
        _app?.Close();
        _app?.Dispose();
    }
}

public static class UiPoll
{
    public static T Until<T>(Func<T?> probe, TimeSpan timeout, string description) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = probe();
            if (value is not null)
            {
                return value;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(description);
    }

    public static void UntilTrue(Func<bool> probe, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (probe())
            {
                return;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(description);
    }
}
