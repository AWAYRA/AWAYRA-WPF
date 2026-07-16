using Awayra.Core.Models;

namespace Awayra.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IStateStore
{
    Task<SchedulerState?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SchedulerState state, CancellationToken cancellationToken = default);
}

public interface IStatisticsStore
{
    Task<StatisticsData> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StatisticsData data, CancellationToken cancellationToken = default);
}

public interface IIdleMonitor
{
    TimeSpan GetIdleTime();
    bool IsIdle(TimeSpan threshold);
}

public interface IAutostartService
{
    bool IsEnabled();
    void Enable(string executablePath);
    void Disable();
    void RepairIfStale(string executablePath);
}

public interface ISingleInstanceCoordinator
{
    bool TryAcquire();
    void SignalExistingInstance();
    void ListenForSignals(Action onSignal);
    void Release();
}

public interface IAppLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    Task FlushAsync();
}
