using Awayra.App.Services;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.App.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ApplicationHostBootTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 30, 10, 0, 0, TimeSpan.FromHours(4));
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"Awayra-AppHostBootTests-{Guid.NewGuid():N}");
        AppPaths.OverrideDataRoot = _tempDir;
    }

    [TestCleanup]
    public void Cleanup()
    {
        AppPaths.OverrideDataRoot = null;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task NewBoot_ResetsPersistedTimersUsingSavedIntervals()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 30;
        settings.MoveBreakIntervalMinutes = 55;
        var state = SchedulerState.CreateDefault(Start.AddDays(-1));
        var currentBoot = Start.ToUniversalTime();
        state.SystemBootStartedAtUtc = currentBoot.AddDays(-1);
        state.EyeNextDue = Start.AddMinutes(-10);
        state.MoveNextDue = Start.AddMinutes(-5);
        state.IsPausedManual = true;
        state.ActiveBreak = BreakType.Eye;
        using var host = CreateHost(settings, state);

        await host.InitializeAsync(currentBoot);

        var snapshot = host.Scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(30).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(TimeSpan.FromMinutes(55).TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
        Assert.IsFalse(snapshot.IsPausedManual);
        Assert.IsNull(snapshot.ActiveBreak);
        Assert.AreEqual(currentBoot, host.Scheduler.State.SystemBootStartedAtUtc);
    }

    [TestMethod]
    public async Task SameBoot_PreservesPersistedTimer()
    {
        var state = SchedulerState.CreateDefault(Start);
        var currentBoot = Start.ToUniversalTime().AddHours(-1);
        state.SystemBootStartedAtUtc = currentBoot;
        state.EyeNextDue = Start.AddMinutes(7);
        using var host = CreateHost(AppSettings.CreateDefault(), state);

        await host.InitializeAsync(currentBoot.AddSeconds(20));

        Assert.AreEqual(TimeSpan.FromMinutes(7).TotalSeconds, host.Scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public async Task LegacyStateFromPreviousBoot_ResetsOnMigration()
    {
        var state = SchedulerState.CreateDefault(Start.AddDays(-1));
        state.SystemBootStartedAtUtc = null;
        state.LastClockCheck = Start.AddDays(-1);
        state.EyeNextDue = Start.AddMinutes(-10);
        using var host = CreateHost(AppSettings.CreateDefault(), state);

        await host.InitializeAsync(Start.ToUniversalTime().AddMinutes(-5));

        Assert.AreEqual(TimeSpan.FromMinutes(20).TotalSeconds, host.Scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
    }

    private static ApplicationHost CreateHost(AppSettings settings, SchedulerState state) =>
        new(
            new NullLogger(),
            new FakeClock(Start),
            new FixedSettingsStore(settings),
            new FixedStateStore(state),
            new InMemoryStatisticsStore(),
            new NullIdleMonitor(),
            new NullAutostartService(),
            new LocalizationService());

    private sealed class FixedSettingsStore(AppSettings settings) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(AppSettings value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedStateStore(SchedulerState state) : IStateStore
    {
        public Task<SchedulerState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SchedulerState?>(state);

        public Task SaveAsync(SchedulerState value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
