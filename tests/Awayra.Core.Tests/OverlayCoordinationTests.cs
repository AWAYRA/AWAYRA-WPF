using Awayra.Core.Coordination;
using Awayra.Core.Models;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class OverlayLayoutCalculatorTests
{
    [TestMethod]
    public void CenterWithinWorkingArea_ReturnsOnScreenRectangle()
    {
        var window = new ScreenBounds(0, 0, 420, 300);
        var workingArea = new ScreenBounds(100, 100, 1600, 900);

        var centered = OverlayLayoutCalculator.CenterWithinWorkingArea(window, workingArea);

        Assert.AreEqual(690, centered.Left, 0.001);
        Assert.AreEqual(400, centered.Top, 0.001);
        Assert.IsTrue(OverlayLayoutCalculator.IsOnAnyWorkingArea(centered, [workingArea]));
    }

    [TestMethod]
    public void SelectMonitorForPoint_HandlesNegativeCoordinates()
    {
        var leftMonitor = new ScreenBounds(-1920, 0, 1920, 1080);
        var primaryMonitor = new ScreenBounds(0, 0, 1920, 1080);
        IReadOnlyList<ScreenBounds> monitors = [leftMonitor, primaryMonitor];

        var selected = OverlayLayoutCalculator.SelectMonitorForPoint(-500, 400, monitors);

        Assert.AreEqual(-1920, selected.Left);
    }

    [TestMethod]
    public void IsOnAnyWorkingArea_FalseWhenOffscreen()
    {
        var offscreen = new ScreenBounds(9000, 9000, 400, 300);
        var workingArea = new ScreenBounds(0, 0, 1920, 1080);

        Assert.IsFalse(OverlayLayoutCalculator.IsOnAnyWorkingArea(offscreen, [workingArea]));
    }
}

[TestClass]
public sealed class OverlaySessionPolicyTests
{
    [TestMethod]
    public void AfterShow_AllowsOnlyOneOverlayType()
    {
        var eye = OverlaySessionPolicy.AfterShow(BreakType.Eye, OverlaySessionState.Empty);
        var move = OverlaySessionPolicy.AfterShow(BreakType.Move, OverlaySessionState.Empty);

        Assert.IsTrue(eye.EyeVisible);
        Assert.IsFalse(eye.MoveVisible);
        Assert.IsFalse(move.EyeVisible);
        Assert.IsTrue(move.MoveVisible);
        Assert.IsTrue(OverlaySessionPolicy.AllowsSimultaneousOverlays(eye));
        Assert.IsTrue(OverlaySessionPolicy.AllowsSimultaneousOverlays(move));
    }

    [TestMethod]
    public void AfterCloseAll_ClearsSession()
    {
        var cleared = OverlaySessionPolicy.AfterCloseAll();

        Assert.IsFalse(cleared.HasAnyVisible);
        Assert.IsFalse(cleared.BothVisible);
    }

    [TestMethod]
    public void RequiresCloseBeforeShow_WhenAnotherOverlayVisible()
    {
        var state = new OverlaySessionState(EyeVisible: true, MoveVisible: false);

        Assert.IsTrue(OverlaySessionPolicy.RequiresCloseBeforeShow(state, BreakType.Move));
    }
}
