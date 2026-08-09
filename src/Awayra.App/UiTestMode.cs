using System.Reflection;
using System.Security.Cryptography;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;

namespace Awayra.App;

public static class UiTestMode
{
    public const string Argument = "--ui-test";
    public const string DataRootArgument = "--ui-test-data-root";

    public static bool IsEnabled { get; private set; }

    public static string? DataRoot { get; private set; }

    public static void Configure(string[] args)
    {
        IsEnabled = args.Any(a => string.Equals(a, Argument, StringComparison.OrdinalIgnoreCase));
        if (!IsEnabled)
        {
            return;
        }

        var dataRootIndex = Array.FindIndex(
            args,
            a => string.Equals(a, DataRootArgument, StringComparison.OrdinalIgnoreCase));
        if (dataRootIndex >= 0 && dataRootIndex + 1 < args.Length &&
            !string.IsNullOrWhiteSpace(args[dataRootIndex + 1]))
        {
            DataRoot = Path.GetFullPath(args[dataRootIndex + 1]);
        }
        else
        {
            DataRoot = Path.Combine(Path.GetTempPath(), $"Awayra-UiTest-{Guid.NewGuid():N}");
        }

        Directory.CreateDirectory(DataRoot);
        AppPaths.OverrideDataRoot = DataRoot;
    }

    public static AppSettings ApplyDefaults(AppSettings settings)
    {
        if (!IsEnabled)
        {
            return settings;
        }

        settings.RunAtStartup = false;
        settings.StartMinimized = false;
        settings.EyeResetEnabled = true;
        settings.EyeResetIntervalMinutes = 1;
        settings.EyeResetDurationSeconds = 10;
        settings.MoveBreakEnabled = true;
        settings.MoveBreakIntervalMinutes = 1;
        settings.MoveBreakDurationSeconds = 10;
        settings.SnoozeDurationMinutes = 1;
        settings.AllowSnooze = true;
        settings.AllowSkip = true;
        settings.PauseWhileIdle = true;
        settings.IdleThresholdMinutes = 1;
        settings.WorkHoursEnabled = false;
        return settings;
    }
}

public static class BuildIdentity
{
    public static string AssemblyVersion { get; private set; } = "unknown";
    public static string InformationalVersion { get; private set; } = "unknown";
    public static string ExecutablePath { get; private set; } = "unknown";
    public static string ExecutableSha256 { get; private set; } = "unknown";
    public static string GitCommit { get; private set; } = "unknown";
    public static string WorkingTreeStatus { get; private set; } = "unknown";
    public static DateTimeOffset BuildTimestampUtc { get; private set; } = DateTimeOffset.UtcNow;

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        AssemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
        InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? AssemblyVersion;
        ExecutablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        ExecutableSha256 = ComputeSha256(ExecutablePath);
        GitCommit = ReadMetadata("GitCommit") ?? "unknown";
        WorkingTreeStatus = ReadMetadata("WorkingTreeStatus") ?? "unknown";
        if (DateTimeOffset.TryParse(ReadMetadata("BuildTimestampUtc"), out var ts))
        {
            BuildTimestampUtc = ts;
        }
    }

    public static void Log(IAppLogger logger)
    {
        logger.Info($"BuildIdentity AssemblyVersion={AssemblyVersion}");
        logger.Info($"BuildIdentity InformationalVersion={InformationalVersion}");
        logger.Info($"BuildIdentity ExecutablePath={ExecutablePath}");
        logger.Info($"BuildIdentity ExecutableSha256={ExecutableSha256}");
        logger.Info($"BuildIdentity ProcessId={Environment.ProcessId}");
        logger.Info($"BuildIdentity GitCommit={GitCommit}");
        logger.Info($"BuildIdentity WorkingTreeStatus={WorkingTreeStatus}");
        logger.Info($"BuildIdentity BuildTimestampUtc={BuildTimestampUtc:O}");
        if (UiTestMode.IsEnabled)
        {
            logger.Info($"BuildIdentity UiTestMode=true DataRoot={UiTestMode.DataRoot}");
        }
    }

    private static string? ReadMetadata(string key) =>
        CurrentAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;

    private static string ComputeSha256(string path)
    {
        if (!File.Exists(path))
        {
            return "missing";
        }

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static Assembly CurrentAssembly => Assembly.GetExecutingAssembly();
}

public sealed class UiTestBridge
{
    public static UiTestBridge? Current { get; private set; }

    public static void Register(UiTestBridge bridge) => Current = bridge;

    public required Action ShowDashboard { get; init; }
    public required Action ShowSettings { get; init; }
    public required Action Quit { get; init; }
    public required Action TriggerEyeNow { get; init; }
    public required Action TriggerMoveNow { get; init; }
    public required Func<AppSettings> GetSettings { get; init; }
    public required Func<string> GetEyeCountdown { get; init; }
    public required Func<string> GetMoveCountdown { get; init; }
}
