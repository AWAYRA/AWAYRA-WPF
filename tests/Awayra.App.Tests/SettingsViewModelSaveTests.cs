using Awayra.App.Services;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.App.Tests;

[TestClass]
public sealed class SettingsViewModelSaveTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 17, 10, 0, 0, TimeSpan.FromHours(4));

    [TestMethod]
    public async Task SaveConfiguration_WithUnchangedSchedule_ResumesFrozenEyeRemaining()
    {
        var clock = new FakeClock(Start);
        var host = CreateHost(clock);
        clock.Advance(TimeSpan.FromMinutes(5));
        host.Scheduler.Tick();

        host.BeginConfigurationSession();
        var frozenEye = host.Scheduler.GetSnapshot().EyeRemaining;
        clock.Advance(TimeSpan.FromSeconds(40));

        await host.SaveConfigurationAsync(CloneSettings(host.Settings));

        Assert.AreEqual(frozenEye.TotalSeconds, host.Scheduler.GetSnapshot().EyeRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public async Task SaveConfiguration_WithEyeIntervalChange_ResetsEyeOnly()
    {
        var clock = new FakeClock(Start);
        var host = CreateHost(clock);
        clock.Advance(TimeSpan.FromMinutes(5));
        host.Scheduler.Tick();

        host.BeginConfigurationSession();
        var frozenMove = host.Scheduler.GetSnapshot().MoveRemaining;
        clock.Advance(TimeSpan.FromSeconds(40));

        var updated = CloneSettings(host.Settings);
        updated.EyeResetIntervalMinutes = 25;
        await host.SaveConfigurationAsync(updated);

        var snapshot = host.Scheduler.GetSnapshot();
        Assert.AreEqual(TimeSpan.FromMinutes(25).TotalSeconds, snapshot.EyeRemaining.TotalSeconds, 1);
        Assert.AreEqual(frozenMove.TotalSeconds, snapshot.MoveRemaining.TotalSeconds, 1);
    }

    [TestMethod]
    public void SettingsScheduleChanges_DetectsOnlySchedulingFields()
    {
        var original = AppSettings.CreateDefault();
        var glassOnly = CloneSettings(original);
        glassOnly.GlassClarity = 130;

        Assert.IsFalse(SettingsScheduleChanges.EyeScheduleChanged(original, glassOnly));
        Assert.IsFalse(SettingsScheduleChanges.MoveScheduleChanged(original, glassOnly));

        var eyeOnly = CloneSettings(original);
        eyeOnly.EyeResetIntervalMinutes = 15;
        Assert.IsTrue(SettingsScheduleChanges.EyeScheduleChanged(original, eyeOnly));
        Assert.IsFalse(SettingsScheduleChanges.MoveScheduleChanged(original, eyeOnly));
    }

    private static ApplicationHost CreateHost(FakeClock clock)
    {
        var host = new ApplicationHost(
            new NullLogger(),
            clock,
            new InMemorySettingsStore(),
            new InMemoryStateStore(),
            new InMemoryStatisticsStore(),
            new NullIdleMonitor(),
            new NullAutostartService(),
            new LocalizationService());

        host.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return host;
    }

    // AppSettings.Copy() rather than a hand-written field list: the previous one silently dropped
    // every sound setting, and would have dropped each new setting added after it.
    private static AppSettings CloneSettings(AppSettings source) => source.Copy();

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
