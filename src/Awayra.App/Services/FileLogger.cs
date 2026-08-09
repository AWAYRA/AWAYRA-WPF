using System.Globalization;
using System.Text;
using Awayra.Core.Abstractions;

namespace Awayra.App.Services;

public sealed class FileLogger : IAppLogger, IDisposable
{
    private const long MaxFileSize = 1_048_576;

    /// <summary>Archived files kept beside the active log, so at most four files in total.</summary>
    private const int MaxArchiveFiles = 3;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _logPath;
    private volatile bool _disposed;

    public FileLogger(string logPath)
    {
        _logPath = logPath;
        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var details = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", details);
    }

    public async Task FlushAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        _gate.Release();
    }

    /// <summary>
    /// Stops accepting writes and releases the gate. The semaphore itself is deliberately left alone:
    /// logging happens from timers and background listeners that can still be unwinding, and
    /// disposing it under them would turn a shutdown into an ObjectDisposedException.
    /// </summary>
    public void Dispose() => _disposed = true;

    private void Write(string level, string message)
    {
        if (_disposed)
        {
            return;
        }

        _gate.Wait();
        try
        {
            RollIfNeeded();

            // Invariant culture: log timestamps are diagnostic records, and a machine on a
            // non-Gregorian calendar would otherwise stamp them in that calendar's year.
            var timestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture);
            File.AppendAllText(_logPath, $"{timestamp} [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch (IOException)
        {
            // Logging must never take the application down with it.
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RollIfNeeded()
    {
        if (!File.Exists(_logPath))
        {
            return;
        }

        var info = new FileInfo(_logPath);
        if (info.Length < MaxFileSize)
        {
            return;
        }

        for (var i = MaxArchiveFiles - 1; i >= 1; i--)
        {
            var source = $"{_logPath}.{i}";
            var target = $"{_logPath}.{i + 1}";
            if (File.Exists(source))
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                File.Move(source, target);
            }
        }

        var firstArchive = $"{_logPath}.1";
        if (File.Exists(firstArchive))
        {
            File.Delete(firstArchive);
        }

        File.Move(_logPath, firstArchive);
    }
}
