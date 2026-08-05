using System.IO;
using System.IO.Pipes;
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
        _pipeName = LocalPipe.NameFor(PipeNamePrefix.TrimEnd('.'));
    }

    public bool TryAcquire()
    {
        var mutexName = $"Local\\{_pipeName}";
        _mutex = new Mutex(true, mutexName, out var createdNew);
        return createdNew;
    }

    /// <summary>
    /// Retries, because the listener recreates its server stream between connections and the first
    /// instance may still be starting up. A single attempt meant launching Awayra again could
    /// silently do nothing instead of bringing the dashboard forward.
    /// </summary>
    public void SignalExistingInstance()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                client.Connect(500);
                using var writer = new StreamWriter(client);
                writer.WriteLine("SHOW");
                writer.Flush();
                return;
            }
            catch (TimeoutException)
            {
                // The listener is between connections or not up yet; try again.
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch
            {
                return;
            }
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
                    using var server = LocalPipe.CreateServer(_pipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using (var reader = new StreamReader(server))
                    {
                        await reader.ReadLineAsync(token).ConfigureAwait(false);
                    }

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
