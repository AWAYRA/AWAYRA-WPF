using System.Text;
using Awayra.Core.Abstractions;

namespace Awayra.App.Services;

public sealed class FileLogger : IAppLogger, IDisposable
{
    private const long MaxFileSize = 1_048_576;
    private const int MaxFiles = 3;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _logPath;

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

    public void Dispose() => _gate.Dispose();

    private void Write(string level, string message)
    {
        _gate.Wait();
        try
        {
            RollIfNeeded();
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(_logPath, line, Encoding.UTF8);
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

        for (var i = MaxFiles - 1; i >= 1; i--)
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
