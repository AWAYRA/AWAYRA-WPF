using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class WorkHoursEvaluatorTests
{
    private static readonly DateTimeOffset Base = new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void SameDayRange_Inside_ReturnsTrue()
    {
        var time = Base.AddHours(10);
        Assert.IsTrue(WorkHoursEvaluator.IsWithinWorkHours(time, true, new TimeOnly(9, 0), new TimeOnly(18, 0)));
    }

    [TestMethod]
    public void SameDayRange_Outside_ReturnsFalse()
    {
        var time = Base.AddHours(20);
        Assert.IsFalse(WorkHoursEvaluator.IsWithinWorkHours(time, true, new TimeOnly(9, 0), new TimeOnly(18, 0)));
    }

    [TestMethod]
    public void OvernightRange_LateNight_ReturnsTrue()
    {
        var time = Base.AddHours(23);
        Assert.IsTrue(WorkHoursEvaluator.IsWithinWorkHours(time, true, new TimeOnly(22, 0), new TimeOnly(6, 0)));
    }

    [TestMethod]
    public void OvernightRange_EarlyMorning_ReturnsTrue()
    {
        var time = Base.AddHours(5);
        Assert.IsTrue(WorkHoursEvaluator.IsWithinWorkHours(time, true, new TimeOnly(22, 0), new TimeOnly(6, 0)));
    }

    [TestMethod]
    public void OvernightRange_Midday_ReturnsFalse()
    {
        var time = Base.AddHours(12);
        Assert.IsFalse(WorkHoursEvaluator.IsWithinWorkHours(time, true, new TimeOnly(22, 0), new TimeOnly(6, 0)));
    }

    [TestMethod]
    public void MidnightBoundary_StartInclusive()
    {
        var time = Base;
        Assert.IsTrue(WorkHoursEvaluator.IsWithinWorkHours(time, true, new TimeOnly(0, 0), new TimeOnly(8, 0)));
    }

    [TestMethod]
    public void Disabled_AlwaysTrue()
    {
        var time = Base.AddHours(3);
        Assert.IsTrue(WorkHoursEvaluator.IsWithinWorkHours(time, false, new TimeOnly(9, 0), new TimeOnly(18, 0)));
    }
}
