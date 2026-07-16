using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using Awayra.Core.Abstractions;

namespace Awayra.App.Services;

public sealed class NamedPipeSingleInstance : ISingleInstanceCoordinator, IDisposable
{
    private const string PipeNamePrefix = "Awayra.SingleInstance.";
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenCts;

    public NamedPipeSingleInstance()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? "default";
        _pipeName = PipeNamePrefix + sid;
    }

    public bool TryAcquire()
    {
        var mutexName = $"Local\\{_pipeName}";
        _mutex = new Mutex(true, mutexName, out var createdNew);
        return createdNew;
    }

    public void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client);
            writer.WriteLine("SHOW");
            writer.Flush();
        }
        catch
        {
            // First instance may be starting; ignore.
        }
    }

    public void ListenForSignals(Action onSignal)
    {
        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    await reader.ReadLineAsync(token).ConfigureAwait(false);
                    onSignal();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
            }
        }, token);
    }

    public void Release()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();
        _listenCts = null;

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Ignore if not owned.
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }

    public void Dispose() => Release();
}
