using System.IO.Pipes;
using System.Text;
using Awayra.App.Services;
using Awayra.Core.Abstractions;

namespace Awayra.App;

public sealed class UiTestCommandPipe : IDisposable
{
    private readonly Action<string> _dispatch;
    private readonly IAppLogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;

    // Scoped to the current account. A fixed machine-wide name left an unauthenticated local
    // channel that could drive or quit the application whenever --ui-test was passed.
    public static readonly string PipeName = LocalPipe.NameFor("Awayra.UiTest.Commands");

    public UiTestCommandPipe(Action<string> dispatch, IAppLogger logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public void Start()
    {
        _listenTask = Task.Run(ListenAsync);
    }

    private async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await using var server = LocalPipe.CreateServer(PipeName, PipeDirection.In);
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var command = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    _logger.Info($"UiTest command received: {command}");
                    _dispatch(command.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning($"UiTest command pipe error: {ex.Message}");
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

    public static void Send(string command)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(5000);
        using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
        writer.WriteLine(command);
    }
}
