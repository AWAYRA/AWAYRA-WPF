using Awayra.Core.Coordination;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class DashboardRestorePlannerTests
{
    [TestMethod]
    public void NotCreated_RequestsCreateShowAndActivate()
    {
        var plan = DashboardRestorePlanner.Plan(DashboardPresentation.NotCreated);

        Assert.IsTrue(plan.CreateIfMissing);
        Assert.IsTrue(plan.Show);
        Assert.IsTrue(plan.RestoreFromMinimized);
        Assert.IsTrue(plan.Activate);
        Assert.IsTrue(plan.EnsureOnScreen);
        Assert.IsTrue(plan.ShowInTaskbar);
    }

    [TestMethod]
    public void Hidden_RequestsShowWithoutCreate()
    {
        var plan = DashboardRestorePlanner.Plan(DashboardPresentation.Hidden);

        Assert.IsFalse(plan.CreateIfMissing);
        Assert.IsTrue(plan.Show);
        Assert.IsTrue(plan.Activate);
    }

    [TestMethod]
    public void Minimized_RequestsRestore()
    {
        var plan = DashboardRestorePlanner.Plan(DashboardPresentation.Minimized);

        Assert.IsFalse(plan.CreateIfMissing);
        Assert.IsTrue(plan.RestoreFromMinimized);
        Assert.IsTrue(plan.Activate);
    }

    [TestMethod]
    public void Visible_ActivatesWithoutShow()
    {
        var plan = DashboardRestorePlanner.Plan(DashboardPresentation.Visible);

        Assert.IsFalse(plan.CreateIfMissing);
        Assert.IsFalse(plan.Show);
        Assert.IsTrue(plan.Activate);
    }

    [TestMethod]
    public void Classify_DistinguishesHiddenAndMinimized()
    {
        Assert.AreEqual(DashboardPresentation.NotCreated, DashboardRestorePlanner.Classify(false, false, false));
        Assert.AreEqual(DashboardPresentation.Hidden, DashboardRestorePlanner.Classify(true, false, false));
        Assert.AreEqual(DashboardPresentation.Minimized, DashboardRestorePlanner.Classify(true, false, true));
        Assert.AreEqual(DashboardPresentation.Visible, DashboardRestorePlanner.Classify(true, true, false));
    }
}
