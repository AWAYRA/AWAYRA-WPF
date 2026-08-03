using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Awayra.Core.Abstractions;
using Forms = System.Windows.Forms;

namespace Awayra.App.Services;

public sealed class DisplayDiagnosticsService : IDisposable
{
    private const long MaxTimelineBytes = 8 * 1024 * 1024;
    private const int MaxTimelineFiles = 4;
    private const int HeartbeatMilliseconds = 2_000;
    private readonly IAppLogger _logger;
    private readonly Channel<QueueItem> _queue = Channel.CreateUnbounded<QueueItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Stopwatch _monotonic = Stopwatch.StartNew();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Task _writerTask;
    private DiagnosticMessageWindow? _messageWindow;
    private System.Threading.Timer? _heartbeatTimer;
    private long _sequence;
    private int _disposed;

    public DisplayDiagnosticsService(IAppLogger logger)
    {
        _logger = logger;
        AppPaths.EnsureDataRoot();
        Directory.CreateDirectory(AppPaths.DiagnosticsDirectory);
        _writerTask = Task.Run(WriterLoopAsync);
    }

    public string TimelinePath => AppPaths.DisplayTimelinePath;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_messageWindow is not null)
        {
            return;
        }

        _messageWindow = new DiagnosticMessageWindow(OnWindowMessage);
        _heartbeatTimer = new System.Threading.Timer(
            _ => CaptureHeartbeat(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(HeartbeatMilliseconds));

        Record("diagnostics", "session_started", new
        {
            sessionId = _sessionId,
            timeline = TimelinePath,
            version = GetApplicationVersion(),
            processId = Environment.ProcessId,
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            machine = Environment.MachineName,
            session = Environment.GetEnvironmentVariable("SESSIONNAME"),
            state = CaptureDesktopState()
        });
        _logger.Info($"Display diagnostics started. Session={_sessionId}; Timeline={TimelinePath}");
    }

    public void Record(string category, string eventName, object? data = null)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var sequence = Interlocked.Increment(ref _sequence);
        var envelope = new DiagnosticEnvelope
        {
            Sequence = sequence,
            SessionId = _sessionId,
            TimestampUtc = DateTimeOffset.UtcNow,
            TimestampLocal = DateTimeOffset.Now,
            MonotonicMilliseconds = _monotonic.Elapsed.TotalMilliseconds,
            ProcessId = Environment.ProcessId,
            ThreadId = Environment.CurrentManagedThreadId,
            Category = category,
            EventName = eventName,
            Data = data
        };

        try
        {
            var line = JsonSerializer.Serialize(envelope, TimelineJsonOptions);
            _queue.Writer.TryWrite(new QueueItem(line, null));
        }
        catch (Exception ex)
        {
            _logger.Warning($"Display diagnostic serialization failed: {ex.Message}");
        }
    }

    public async Task<string> CaptureBlinkReportAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var markerTime = DateTimeOffset.Now;
        var markerState = CaptureDesktopState();
        Record("user", "screen_blink_reported", new { markerTime, markerState });
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        var reportName = $"Awayra-Display-Diagnostics-{markerTime:yyyyMMdd-HHmmss}";
        var workingDirectory = Path.Combine(AppPaths.DiagnosticsDirectory, reportName);
        var reportPath = Path.Combine(AppPaths.DiagnosticsDirectory, reportName + ".zip");

        if (Directory.Exists(workingDirectory))
        {
            Directory.Delete(workingDirectory, recursive: true);
        }

        Directory.CreateDirectory(workingDirectory);
        try
        {
            CopyDiagnosticFiles(workingDirectory);

            var summary = new
            {
                reportVersion = 1,
                markerTime,
                markerTimeUtc = markerTime.ToUniversalTime(),
                sessionId = _sessionId,
                appVersion = GetApplicationVersion(),
                processId = Environment.ProcessId,
                processUptimeSeconds = _monotonic.Elapsed.TotalSeconds,
                os = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                machine = Environment.MachineName,
                session = Environment.GetEnvironmentVariable("SESSIONNAME"),
                userInteractive = Environment.UserInteractive,
                currentState = markerState,
                privacy = "Local diagnostic data only. Foreground process name is recorded, but window titles and file contents are not collected."
            };
            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "summary.json"),
                JsonSerializer.Serialize(summary, ReportJsonOptions),
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "README.txt"),
                "Awayra display diagnostics\r\n\r\n" +
                "This ZIP was created when the user reported a visible screen blink.\r\n" +
                "The JSONL timeline uses local, UTC, and monotonic timestamps so Awayra, Windows, DWM, power, device, and monitor events can be correlated.\r\n" +
                "Each timeline event is exactly one JSON object per line.\r\n" +
                "Send the complete ZIP without editing individual files.\r\n",
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);

            var collectors = new List<Task>
            {
                RunCommandCaptureAsync("wevtutil.exe", ["qe", "System", "/q:*[System[TimeCreated[timediff(@SystemTime) <= 1800000]]]", "/f:text", "/c:500", "/rd:true"], Path.Combine(workingDirectory, "eventlog-system.txt"), TimeSpan.FromSeconds(20), cancellationToken),
                RunCommandCaptureAsync("wevtutil.exe", ["qe", "Application", "/q:*[System[TimeCreated[timediff(@SystemTime) <= 1800000]]]", "/f:text", "/c:300", "/rd:true"], Path.Combine(workingDirectory, "eventlog-application.txt"), TimeSpan.FromSeconds(20), cancellationToken),
                RunCommandCaptureAsync("wevtutil.exe", ["qe", "Microsoft-Windows-DxgKrnl/Operational", "/q:*[System[TimeCreated[timediff(@SystemTime) <= 1800000]]]", "/f:text", "/c:500", "/rd:true"], Path.Combine(workingDirectory, "eventlog-dxgkrnl.txt"), TimeSpan.FromSeconds(20), cancellationToken),
                RunCommandCaptureAsync("wevtutil.exe", ["qe", "Microsoft-Windows-Kernel-PnP/Configuration", "/q:*[System[TimeCreated[timediff(@SystemTime) <= 1800000]]]", "/f:text", "/c:300", "/rd:true"], Path.Combine(workingDirectory, "eventlog-kernel-pnp.txt"), TimeSpan.FromSeconds(20), cancellationToken),
                RunCommandCaptureAsync("pnputil.exe", ["/enum-devices", "/class", "Monitor", "/connected"], Path.Combine(workingDirectory, "devices-monitor.txt"), TimeSpan.FromSeconds(20), cancellationToken),
                RunCommandCaptureAsync("pnputil.exe", ["/enum-devices", "/class", "Display", "/connected"], Path.Combine(workingDirectory, "devices-display-adapter.txt"), TimeSpan.FromSeconds(20), cancellationToken),
                RunCommandCaptureAsync("powercfg.exe", ["/getactivescheme"], Path.Combine(workingDirectory, "power-active-scheme.txt"), TimeSpan.FromSeconds(15), cancellationToken),
                RunCommandCaptureAsync("powercfg.exe", ["/query", "SCHEME_CURRENT", "SUB_VIDEO"], Path.Combine(workingDirectory, "power-display-settings.txt"), TimeSpan.FromSeconds(15), cancellationToken),
                RunDxDiagAsync(Path.Combine(workingDirectory, "dxdiag.txt"), cancellationToken)
            };
            await Task.WhenAll(collectors).ConfigureAwait(false);

            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }

            ZipFile.CreateFromDirectory(workingDirectory, reportPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            Record("diagnostics", "report_created", new { markerTime, reportPath });
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            _logger.Info($"Display diagnostic report created: {reportPath}");
            return reportPath;
        }
        catch (Exception ex)
        {
            Record("diagnostics", "report_failed", new { markerTime, exception = ex.ToString() });
            _logger.Error("Display diagnostic report creation failed", ex);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Could not delete temporary diagnostic directory: {ex.Message}");
            }
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync(new QueueItem(null, completion), cancellationToken).ConfigureAwait(false);
        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _messageWindow?.Dispose();
        _messageWindow = null;

        _queue.Writer.TryComplete();
        try
        {
            if (!_writerTask.Wait(TimeSpan.FromSeconds(2)))
            {
                _cancellation.Cancel();
            }
        }
        catch
        {
            _cancellation.Cancel();
        }

        _cancellation.Dispose();
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_cancellation.Token).ConfigureAwait(false))
            {
                if (item.Line is not null)
                {
                    AppendTimelineLine(item.Line);
                }

                item.Completion?.TrySetResult(true);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error("Display diagnostic writer failed", ex);
        }
    }

    private static void AppendTimelineLine(string line)
    {
        Directory.CreateDirectory(AppPaths.DiagnosticsDirectory);
        RollTimelineIfNeeded();
        File.AppendAllText(AppPaths.DisplayTimelinePath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void RollTimelineIfNeeded()
    {
        if (!File.Exists(AppPaths.DisplayTimelinePath) || new FileInfo(AppPaths.DisplayTimelinePath).Length < MaxTimelineBytes)
        {
            return;
        }

        for (var index = MaxTimelineFiles - 1; index >= 1; index--)
        {
            var source = $"{AppPaths.DisplayTimelinePath}.{index}";
            var target = $"{AppPaths.DisplayTimelinePath}.{index + 1}";
            if (!File.Exists(source))
            {
                continue;
            }

            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(source, target);
        }

        var firstArchive = AppPaths.DisplayTimelinePath + ".1";
        if (File.Exists(firstArchive))
        {
            File.Delete(firstArchive);
        }

        File.Move(AppPaths.DisplayTimelinePath, firstArchive);
    }

    private void CaptureHeartbeat()
    {
        try
        {
            Record("heartbeat", "desktop_state", CaptureDesktopState());
        }
        catch (Exception ex)
        {
            _logger.Warning($"Display diagnostic heartbeat failed: {ex.Message}");
        }
    }

    private void OnWindowMessage(int message, nint wParam, nint lParam)
    {
        var name = message switch
        {
            0x001A => "WM_SETTINGCHANGE",
            0x007E => "WM_DISPLAYCHANGE",
            0x0218 => "WM_POWERBROADCAST",
            0x0219 => "WM_DEVICECHANGE",
            0x02E0 => "WM_DPICHANGED",
            0x031E => "WM_DWMCOMPOSITIONCHANGED",
            _ => null
        };
        if (name is null)
        {
            return;
        }

        var displayWidth = message == 0x007E ? LowWord(lParam) : (int?)null;
        var displayHeight = message == 0x007E ? HighWord(lParam) : (int?)null;
        Record("windows_message", name, new
        {
            message = $"0x{message:X4}",
            wParam = $"0x{wParam.ToInt64():X}",
            lParam = $"0x{lParam.ToInt64():X}",
            bitsPerPixel = message == 0x007E ? wParam.ToInt64() : (long?)null,
            displayWidth,
            displayHeight,
            state = CaptureDesktopState()
        });
    }

    private static object CaptureDesktopState()
    {
        var screens = Forms.Screen.AllScreens.Select(screen =>
        {
            var mode = GetDisplayMode(screen.DeviceName);
            return new
            {
                screen.DeviceName,
                screen.Primary,
                bounds = RectangleData(screen.Bounds),
                workingArea = RectangleData(screen.WorkingArea),
                screen.BitsPerPixel,
                mode
            };
        }).ToArray();

        var cursor = Forms.Cursor.Position;
        var foreground = CaptureForegroundProcess();
        var power = Forms.SystemInformation.PowerStatus;
        using var process = Process.GetCurrentProcess();
        return new
        {
            screenCount = screens.Length,
            screens,
            cursor = new { cursor.X, cursor.Y },
            cursorScreen = Forms.Screen.FromPoint(cursor).DeviceName,
            dwmCompositionEnabled = IsDwmCompositionEnabled(),
            foreground,
            power = new
            {
                lineStatus = power.PowerLineStatus.ToString(),
                batteryChargeStatus = power.BatteryChargeStatus.ToString(),
                batteryPercent = power.BatteryLifePercent,
                batteryLifeRemainingSeconds = power.BatteryLifeRemaining
            },
            awayraProcess = new
            {
                process.Id,
                workingSetBytes = process.WorkingSet64,
                privateMemoryBytes = process.PrivateMemorySize64,
                handleCount = process.HandleCount,
                responding = process.Responding
            }
        };
    }

    private static object? CaptureForegroundProcess()
    {
        try
        {
            var handle = GetForegroundWindow();
            if (handle == nint.Zero)
            {
                return null;
            }

            _ = GetWindowThreadProcessId(handle, out var processId);
            using var process = Process.GetProcessById((int)processId);
            return new { processId, processName = process.ProcessName };
        }
        catch
        {
            return null;
        }
    }

    private static object? GetDisplayMode(string deviceName)
    {
        var mode = new DevMode { DeviceName = new string('\0', 32), FormName = new string('\0', 32) };
        mode.Size = (short)Marshal.SizeOf<DevMode>();
        if (!EnumDisplaySettings(deviceName, -1, ref mode))
        {
            return null;
        }

        return new
        {
            width = mode.PelsWidth,
            height = mode.PelsHeight,
            frequencyHz = mode.DisplayFrequency,
            bitsPerPixel = mode.BitsPerPel,
            orientation = mode.DisplayOrientation,
            positionX = mode.PositionX,
            positionY = mode.PositionY
        };
    }

    private static object RectangleData(System.Drawing.Rectangle rectangle) => new
    {
        rectangle.Left,
        rectangle.Top,
        rectangle.Width,
        rectangle.Height,
        rectangle.Right,
        rectangle.Bottom
    };

    private static bool? IsDwmCompositionEnabled()
    {
        try
        {
            return DwmIsCompositionEnabled(out var enabled) == 0 ? enabled : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(DisplayDiagnosticsService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static void CopyDiagnosticFiles(string destination)
    {
        for (var index = 0; index <= MaxTimelineFiles; index++)
        {
            var source = index == 0 ? AppPaths.DisplayTimelinePath : $"{AppPaths.DisplayTimelinePath}.{index}";
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(destination, Path.GetFileName(source)), overwrite: true);
            }
        }

        if (Directory.Exists(AppPaths.LogsDirectory))
        {
            foreach (var source in Directory.EnumerateFiles(AppPaths.LogsDirectory, "awayra.log*"))
            {
                File.Copy(source, Path.Combine(destination, Path.GetFileName(source)), overwrite: true);
            }
        }
    }

    private static async Task RunCommandCaptureAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string outputPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = info };
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }

            var output = await standardOutput.ConfigureAwait(false);
            var error = await standardError.ConfigureAwait(false);
            await File.WriteAllTextAsync(
                outputPath,
                $"Command: {executable} {string.Join(" ", arguments)}{Environment.NewLine}" +
                $"ExitCode: {(process.HasExited ? process.ExitCode : -1)}{Environment.NewLine}{Environment.NewLine}" +
                output + Environment.NewLine + "--- STDERR ---" + Environment.NewLine + error,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(outputPath, $"Collector failed: {ex}", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RunDxDiagAsync(string outputPath, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dxdiag.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("/dontskip");
        info.ArgumentList.Add("/whql:off");
        info.ArgumentList.Add("/t");
        info.ArgumentList.Add(outputPath);

        try
        {
            using var process = new Process { StartInfo = info };
            process.Start();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(45));
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }

            if (!File.Exists(outputPath))
            {
                await File.WriteAllTextAsync(outputPath, "DxDiag did not create an output file before the timeout.", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(outputPath, $"DxDiag collector failed: {ex}", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
    }

    private static int LowWord(nint value) => unchecked((ushort)(long)value);
    private static int HighWord(nint value) => unchecked((ushort)((long)value >> 16));

    private sealed class DiagnosticMessageWindow : Forms.NativeWindow, IDisposable
    {
        private readonly Action<int, nint, nint> _callback;

        public DiagnosticMessageWindow(Action<int, nint, nint> callback)
        {
            _callback = callback;
            CreateHandle(new Forms.CreateParams
            {
                Caption = "Awayra.DisplayDiagnostics",
                Parent = new nint(-3)
            });
        }

        protected override void WndProc(ref Forms.Message message)
        {
            _callback(message.Msg, message.WParam, message.LParam);
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (Handle != nint.Zero)
            {
                DestroyHandle();
            }
        }
    }

    private sealed record QueueItem(string? Line, TaskCompletionSource<bool>? Completion);

    private sealed class DiagnosticEnvelope
    {
        public long Sequence { get; init; }
        public Guid SessionId { get; init; }
        public DateTimeOffset TimestampUtc { get; init; }
        public DateTimeOffset TimestampLocal { get; init; }
        public double MonotonicMilliseconds { get; init; }
        public int ProcessId { get; init; }
        public int ThreadId { get; init; }
        public required string Category { get; init; }
        public required string EventName { get; init; }
        public object? Data { get; init; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public int Fields;
        public int PositionX;
        public int PositionY;
        public int DisplayOrientation;
        public int DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public short LogPixels;
        public int BitsPerPel;
        public int PelsWidth;
        public int PelsHeight;
        public int DisplayFlags;
        public int DisplayFrequency;
        public int ICMMethod;
        public int ICMIntent;
        public int MediaType;
        public int DitherType;
        public int Reserved1;
        public int Reserved2;
        public int PanningWidth;
        public int PanningHeight;
    }

    private static readonly JsonSerializerOptions TimelineJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DevMode deviceMode);

    [DllImport("dwmapi.dll")]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);
}
