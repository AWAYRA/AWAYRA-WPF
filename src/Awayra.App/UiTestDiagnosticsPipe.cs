using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Awayra.App.Services;
using Awayra.Core.Abstractions;
using Awayra.Core.Services;

namespace Awayra.App;

public sealed class UiTestDiagnosticsPipe : IDisposable
{
    private readonly Func<SchedulerDiagnostics> _diagnosticsProvider;
    private readonly IAppLogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;

    // Scoped to the current account. A fixed machine-wide name left an unauthenticated local
    // channel that could drive or quit the application whenever --ui-test was passed.
    public static readonly string PipeName = LocalPipe.NameFor("Awayra.UiTest.Diagnostics");

    public UiTestDiagnosticsPipe(Func<SchedulerDiagnostics> diagnosticsProvider, IAppLogger logger)
    {
        _diagnosticsProvider = diagnosticsProvider;
        _logger = logger;
    }

    public void Start() => _listenTask = Task.Run(ListenAsync);

    private async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await using var server = LocalPipe.CreateServer(PipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                var command = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (string.Equals(command, "QUERY", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JsonSerializer.Serialize(_diagnosticsProvider(), Awayra.Core.Persistence.JsonOptions.Create());
                    await writer.WriteLineAsync(json).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning($"UiTest diagnostics pipe error: {ex.Message}");
                await Task.Delay(100, _cts.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }

    public static SchedulerDiagnostics Query(TimeSpan timeout)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
        client.Connect((int)timeout.TotalMilliseconds);
        using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
        writer.WriteLine("QUERY");
        var json = reader.ReadLine() ?? "{}";
        return JsonSerializer.Deserialize<SchedulerDiagnostics>(json) ?? new SchedulerDiagnostics();
    }
}
