using Awayra.App.Services;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.App.Tests;

/// <summary>
/// Reproduces the tray-quit sequence against real file-backed stores. QuitFromTray calls
/// Shutdown to stop the timers, then PersistAllAsync for the final save, and the stores are
/// only disposed later when OnExit disposes the host. Disposing the stores inside Shutdown
/// turned that final save into an ObjectDisposedException on every tray quit.
/// </summary>
[TestClass]
public sealed class QuitPersistenceTests
{
    private string _tempDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "awayra-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task PersistAllAsync_AfterShutdown_StillWritesEveryStore()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var statePath = Path.Combine(_tempDir, "state.json");
        var statsPath = Path.Combine(_tempDir, "stats.json");

        var host = new ApplicationHost(
            new NullLogger(),
            new FakeClock(new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero)),
            new SettingsFileStore(new JsonFileStore<AppSettings>(
                settingsPath, new NullLogger(), AppSettings.CreateDefault)),
            new TempFileStateStore(statePath),
            new StatisticsFileStore(new JsonFileStore<StatisticsData>(
                statsPath, new NullLogger(), StatisticsData.CreateDefault)),
            new NullIdleMonitor(),
            new NullAutostartService(),
            new LocalizationService(),
            breakSound: NullBreakSoundService.Instance);

        await host.InitializeAsync();

        // The exact tray-quit order: stop activity first, then write the final snapshot.
        host.Shutdown();
        await host.PersistAllAsync();

        Assert.IsTrue(File.Exists(settingsPath), "Settings were not persisted after Shutdown.");
        Assert.IsTrue(File.Exists(statePath), "Scheduler state was not persisted after Shutdown.");
        Assert.IsTrue(File.Exists(statsPath), "Statistics were not persisted after Shutdown.");

        host.Dispose();
    }

    private sealed class TempFileStateStore : IStateStore, IDisposable
    {
        private readonly JsonFileStore<SchedulerState> _store;
        private readonly string _path;

        public TempFileStateStore(string path)
        {
            _path = path;
            _store = new JsonFileStore<SchedulerState>(
                path, new NullLogger(), () => SchedulerState.CreateDefault(DateTimeOffset.Now));
        }

        public async Task<SchedulerState?> LoadAsync(CancellationToken cancellationToken = default) =>
            File.Exists(_path) ? await _store.LoadAsync(cancellationToken).ConfigureAwait(false) : null;

        public Task SaveAsync(SchedulerState state, CancellationToken cancellationToken = default) =>
            _store.SaveAsync(state, cancellationToken);

        public void Dispose() => _store.Dispose();
    }

    private sealed class NullIdleMonitor : IIdleMonitor
    {
        public TimeSpan GetIdleTime() => TimeSpan.Zero;
        public bool IsIdle(TimeSpan threshold) => false;
    }

    private sealed class NullAutostartService : IAutostartService
    {
        public bool IsEnabled() => false;
        public void Enable(string executablePath) { }
        public void Disable() { }
        public void RepairIfStale(string executablePath) { }
    }
}
