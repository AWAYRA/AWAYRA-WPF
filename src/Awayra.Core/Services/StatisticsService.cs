using Awayra.Core.Abstractions;
using Awayra.Core.Models;

namespace Awayra.Core.Services;

public sealed class StatisticsService
{
    private readonly IClock _clock;
    private StatisticsData _data;

    public StatisticsService(IClock clock, StatisticsData? initial = null)
    {
        _clock = clock;
        _data = initial ?? StatisticsData.CreateDefault();
        EnsureToday();
    }

    public StatisticsData Data => _data;

    public DailyStatistics GetToday()
    {
        EnsureToday();
        var key = GetDayKey(_clock.Now);
        if (!_data.Days.TryGetValue(key, out var stats))
        {
            stats = new DailyStatistics();
            _data.Days[key] = stats;
        }

        return stats;
    }

    public void RecordCompletion(BreakType breakType)
    {
        var today = GetToday();
        if (breakType == BreakType.Eye)
        {
            today.EyeCompleted++;
        }
        else
        {
            today.MoveCompleted++;
        }
    }

    public void RecordSkip()
    {
        GetToday().Skipped++;
    }

    public void RecordSnooze()
    {
        GetToday().Snoozed++;
    }

    public void ReplaceData(StatisticsData data)
    {
        _data = data;
        EnsureToday();
    }

    private void EnsureToday()
    {
        var key = GetDayKey(_clock.Now);
        if (!_data.Days.ContainsKey(key))
        {
            _data.Days[key] = new DailyStatistics();
        }
    }

    public static string GetDayKey(DateTimeOffset time) => time.ToString("yyyy-MM-dd");
}
