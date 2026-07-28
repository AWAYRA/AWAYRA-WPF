using System.Text.Json;
using Awayra.Core.Services;

namespace Awayra.App;

public static class UiTestDiagnosticsWriter
{
    private static readonly object Gate = new();
    private static string? _path;

    public static void Initialize(string dataRoot)
    {
        _path = Path.Combine(dataRoot, "diagnostics.json");
    }

    public static void Write(SchedulerDiagnostics diagnostics)
    {
        if (_path is null)
        {
            return;
        }

        lock (Gate)
        {
            var json = JsonSerializer.Serialize(diagnostics, Awayra.Core.Persistence.JsonOptions.Create());
            File.WriteAllText(_path, json);
        }
    }

    public static string? DiagnosticsPath => _path;
}
