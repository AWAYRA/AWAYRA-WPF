using Awayra.App.Services;
using Awayra.App.ViewModels;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.App.Tests;

[TestClass]
public sealed class SettingsInputValidationTests
{
    [TestMethod]
    public async Task Save_InvalidWorkTime_DoesNotCloseOrPersist()
    {
        var settingsStore = new TrackingSettingsStore();
        using var host = new ApplicationHost(
            new NullLogger(),
            new FakeClock(DateTimeOffset.Now),
            settingsStore,
            new InMemoryStateStore(),
            new InMemoryStatisticsStore(),
            new NullIdleMonitor(),
            new NullAutostartService(),
            new LocalizationService());
        var closed = false;
        var viewModel = new SettingsViewModel(host, _ => closed = true)
        {
            WorkHoursEnabled = true,
            WorkStart = "99:99",
            WorkEnd = "18:00"
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.IsFalse(closed);
        Assert.AreEqual(0, settingsStore.SaveCount);
        Assert.AreEqual(1, viewModel.ValidationErrors.Count);
        StringAssert.Contains(viewModel.ValidationErrors[0], "24-hour time");
    }

    private sealed class TrackingSettingsStore : ISettingsStore
    {
        public int SaveCount { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AppSettings.CreateDefault());

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
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
