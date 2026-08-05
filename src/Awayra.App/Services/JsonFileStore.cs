using System.Text.Json;
using Awayra.Core.Abstractions;
using Awayra.Core.Persistence;

namespace Awayra.App.Services;

public sealed class JsonFileStore<T> : IDisposable where T : class, new()
{
    private readonly string _path;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<T> _defaultFactory;
    private readonly Func<string, T>? _recoveryLoader;

    public JsonFileStore(string path, IAppLogger logger, Func<T> defaultFactory, Func<string, T>? recoveryLoader = null)
    {
        _path = path;
        _logger = logger;
        _defaultFactory = defaultFactory;
        _recoveryLoader = recoveryLoader;
    }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return _defaultFactory();
            }

            var json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
            try
            {
                var value = JsonSerializer.Deserialize<T>(json, JsonOptions.Create());
                return value ?? _defaultFactory();
            }
            catch (JsonException ex)
            {
                _logger.Warning($"Corrupt JSON at {_path}: {ex.Message}");
                BackupCorrupt(json);
                if (_recoveryLoader is not null)
                {
                    return _recoveryLoader(json);
                }

                return _defaultFactory();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _path + ".tmp";
            var json = JsonSerializer.Serialize(value, JsonOptions.Create());
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

            if (File.Exists(_path))
            {
                File.Replace(tempPath, _path, _path + ".bak");
            }
            else
            {
                File.Move(tempPath, _path);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save {_path}", ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private void BackupCorrupt(string json)
    {
        try
        {
            var backup = $"{_path}.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmss}";
            File.WriteAllText(backup, json);
            _logger.Info($"Backed up corrupt file to {backup}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to backup corrupt file", ex);
        }
    }
}

public sealed class SettingsFileStore : ISettingsStore, IDisposable
{
    private readonly JsonFileStore<Core.Models.AppSettings> _store;

    public SettingsFileStore(JsonFileStore<Core.Models.AppSettings> store) => _store = store;

    public Task<Core.Models.AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);

    public Task SaveAsync(Core.Models.AppSettings settings, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(settings, cancellationToken);

    public void Dispose() => _store.Dispose();
}

public sealed class StateFileStore : IStateStore, IDisposable
{
    private readonly JsonFileStore<Core.Models.SchedulerState> _store;

    public StateFileStore(JsonFileStore<Core.Models.SchedulerState> store) => _store = store;

    public async Task<Core.Models.SchedulerState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AppPaths.StatePath))
        {
            return null;
        }

        return await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SaveAsync(Core.Models.SchedulerState state, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(state, cancellationToken);

    public void Dispose() => _store.Dispose();
}

public sealed class StatisticsFileStore : IStatisticsStore, IDisposable
{
    private readonly JsonFileStore<Core.Models.StatisticsData> _store;

    public StatisticsFileStore(JsonFileStore<Core.Models.StatisticsData> store) => _store = store;

    public Task<Core.Models.StatisticsData> LoadAsync(CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);

    public Task SaveAsync(Core.Models.StatisticsData data, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(data, cancellationToken);

    public void Dispose() => _store.Dispose();
}
