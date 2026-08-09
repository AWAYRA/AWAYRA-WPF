using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class StatisticsServiceTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 7, 18, 1, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void CompletionCounters_IncrementCorrectly()
    {
        var clock = new FakeClock(Day1);
        var service = new StatisticsService(clock);

        service.RecordCompletion(BreakType.Eye);
        service.RecordCompletion(BreakType.Move);

        var today = service.GetToday();
        Assert.AreEqual(1, today.EyeCompleted);
        Assert.AreEqual(1, today.MoveCompleted);
    }

    [TestMethod]
    public void SkipAndSnoozeCounters_Increment()
    {
        var clock = new FakeClock(Day1);
        var service = new StatisticsService(clock);

        service.RecordSkip();
        service.RecordSnooze();

        var today = service.GetToday();
        Assert.AreEqual(1, today.Skipped);
        Assert.AreEqual(1, today.Snoozed);
    }

    [TestMethod]
    public void MidnightRollover_CreatesNewDay()
    {
        var clock = new FakeClock(Day1);
        var service = new StatisticsService(clock);
        service.RecordCompletion(BreakType.Eye);

        clock.Set(Day2);
        var today = service.GetToday();

        Assert.AreEqual(0, today.EyeCompleted);
        Assert.IsTrue(service.Data.Days.ContainsKey("2026-07-17"));
        Assert.IsTrue(service.Data.Days.ContainsKey("2026-07-18"));
    }

    [TestMethod]
    public void Persistence_PreservesHistoricalDays()
    {
        var clock = new FakeClock(Day1);
        var service = new StatisticsService(clock);
        service.RecordCompletion(BreakType.Eye);

        clock.Set(Day2);
        service.GetToday();

        Assert.AreEqual(1, service.Data.Days["2026-07-17"].EyeCompleted);
    }

    [TestMethod]
    public void CorruptionRecovery_ReplacesWithEmptyToday()
    {
        var clock = new FakeClock(Day1);
        var data = StatisticsData.CreateDefault();
        data.Days.Clear();
        var service = new StatisticsService(clock, data);

        var today = service.GetToday();
        Assert.AreEqual(0, today.EyeCompleted);
    }
}
