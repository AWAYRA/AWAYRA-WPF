namespace Awayra.Core.Services;

public static class WorkHoursEvaluator
{
    public static bool IsWithinWorkHours(DateTimeOffset localTime, bool enabled, TimeOnly start, TimeOnly end)
    {
        if (!enabled)
        {
            return true;
        }

        var time = TimeOnly.FromDateTime(localTime.DateTime);

        if (start < end)
        {
            return time >= start && time < end;
        }

        if (start > end)
        {
            return time >= start || time < end;
        }

        return false;
    }
}
