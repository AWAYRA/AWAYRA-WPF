using Awayra.Core.Coordination;
using Awayra.Core.Models;

namespace Awayra.Core.Tests;

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
        Assert.IsFalse(eye.BothVisible);
        Assert.IsFalse(move.BothVisible);
    }

    [TestMethod]
    public void AfterShow_ReplacesAPreviouslyVisibleOverlay()
    {
        var eye = OverlaySessionPolicy.AfterShow(BreakType.Eye, OverlaySessionState.Empty);
        var move = OverlaySessionPolicy.AfterShow(BreakType.Move, eye);

        Assert.IsFalse(move.EyeVisible);
        Assert.IsTrue(move.MoveVisible);
    }

    [TestMethod]
    public void AfterCloseAll_ClearsSession()
    {
        var cleared = OverlaySessionPolicy.AfterCloseAll();

        Assert.IsFalse(cleared.HasAnyVisible);
        Assert.IsFalse(cleared.BothVisible);
    }
}
