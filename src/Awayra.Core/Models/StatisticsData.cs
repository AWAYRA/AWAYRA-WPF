namespace Awayra.Core.Models;

public sealed class DailyStatistics
{
    public int EyeCompleted { get; set; }
    public int MoveCompleted { get; set; }
    public int Skipped { get; set; }
    public int Snoozed { get; set; }
}

public sealed class StatisticsData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public Dictionary<string, DailyStatistics> Days { get; set; } = new(StringComparer.Ordinal);

    public static StatisticsData CreateDefault() => new();
}
