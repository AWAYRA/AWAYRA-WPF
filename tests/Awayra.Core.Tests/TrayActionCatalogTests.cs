using Awayra.Core.Coordination;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class TrayActionCatalogTests
{
    [TestMethod]
    public void OpenAwayra_RequestsDashboardRestore()
    {
        Assert.IsTrue(TrayActionCatalog.RequestsDashboardRestore(TrayUserAction.OpenDashboard));
        Assert.AreEqual(TrayUserAction.OpenDashboard, TrayActionCatalog.LeftClickAction);
        Assert.AreEqual(TrayUserAction.OpenDashboard, TrayActionCatalog.DoubleClickAction);
    }

    [TestMethod]
    public void Settings_RequestsSettingsWindow()
    {
        Assert.IsTrue(TrayActionCatalog.RequestsSettings(TrayUserAction.OpenSettings));
    }

    [TestMethod]
    public void BreakNowActions_RequestOverlays()
    {
        Assert.IsTrue(TrayActionCatalog.RequestsEyeOverlay(TrayUserAction.EyeResetNow));
        Assert.IsTrue(TrayActionCatalog.RequestsMoveOverlay(TrayUserAction.MoveBreakNow));
    }

    [TestMethod]
    public void Quit_RequestsShutdown()
    {
        Assert.IsTrue(TrayActionCatalog.RequestsShutdown(TrayUserAction.Quit));
    }

    [TestMethod]
    public void MenuOrder_ContainsExpectedActions()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                TrayUserAction.OpenDashboard,
                TrayUserAction.EyeResetNow,
                TrayUserAction.MoveBreakNow,
                TrayUserAction.TogglePause,
                TrayUserAction.OpenSettings,
                TrayUserAction.Quit
            },
            TrayActionCatalog.MenuActionsInOrder.ToArray());
    }
}
