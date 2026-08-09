using System.Text.Json;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.UiTests.Support;

public sealed class UiTestDiagnosticsClient
{
    private static string? _dataRoot;

    public static void UseDataRoot(string? dataRoot) => _dataRoot = dataRoot;

    public static SchedulerDiagnostics ReadLatest() => ReadFromFile();

    public static SchedulerDiagnostics WaitUntil(Func<SchedulerDiagnostics, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var snapshot = ReadFromFile();
                if (predicate(snapshot))
                {
                    return snapshot;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Diagnostics predicate not satisfied. Last error: {lastError?.Message}");
    }

    private static SchedulerDiagnostics ReadFromFile()
    {
        var dataRoot = _dataRoot;
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            throw new InvalidOperationException("UI test data root is not configured.");
        }

        var path = Path.Combine(dataRoot, "diagnostics.json");
        if (!File.Exists(path))
        {
            return new SchedulerDiagnostics();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SchedulerDiagnostics>(json, JsonOptions.Create())
            ?? new SchedulerDiagnostics();
    }
}
