using Awayra.Core.Coordination;
using Awayra.Core.Models;
using Awayra.Core.Persistence;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class ApplicationStartupPolicyTests
{
    [TestMethod]
    public void StartMinimizedFalse_RequestsDashboardShow()
    {
        var settings = AppSettings.CreateDefault();
        settings.StartMinimized = false;

        Assert.IsTrue(ApplicationStartupPolicy.ShouldShowDashboardOnStartup(settings));
    }

    [TestMethod]
    public void StartMinimizedTrue_RequestsTrayOnlyStartup()
    {
        var settings = AppSettings.CreateDefault();
        settings.StartMinimized = true;

        Assert.IsFalse(ApplicationStartupPolicy.ShouldShowDashboardOnStartup(settings));
    }

    [TestMethod]
    public void CloseToTray_DoesNotImplyStartMinimized()
    {
        var settings = AppSettings.CreateDefault();

        Assert.IsTrue(settings.CloseToTray);
        Assert.IsFalse(settings.StartMinimized);
        Assert.IsTrue(ApplicationStartupPolicy.ShouldShowDashboardOnStartup(settings));
    }

    [TestMethod]
    public void CloseToTray_Enabled_HidesDashboardOnClose()
    {
        var settings = AppSettings.CreateDefault();
        settings.CloseToTray = true;

        Assert.IsTrue(ApplicationStartupPolicy.ShouldHideDashboardToTrayOnClose(settings, isQuitting: false));
    }

    [TestMethod]
    public void Quitting_IgnoresCloseToTray()
    {
        var settings = AppSettings.CreateDefault();
        settings.CloseToTray = true;

        Assert.IsFalse(ApplicationStartupPolicy.ShouldHideDashboardToTrayOnClose(settings, isQuitting: true));
    }

    [TestMethod]
    public void MalformedSettings_DefaultStartMinimizedFalse()
    {
        var recovered = SettingsRecovery.LoadWithRecovery("not json at all");

        Assert.IsFalse(recovered.StartMinimized);
        Assert.IsTrue(ApplicationStartupPolicy.ShouldShowDashboardOnStartup(recovered));
    }

    [TestMethod]
    public void ShouldCreateDashboard_OnlyWhenMissing()
    {
        Assert.IsTrue(ApplicationStartupPolicy.ShouldCreateDashboard(dashboardAlreadyExists: false));
        Assert.IsFalse(ApplicationStartupPolicy.ShouldCreateDashboard(dashboardAlreadyExists: true));
    }

    [TestMethod]
    public void ShouldCreateTrayService_OnlyOnce()
    {
        Assert.IsTrue(ApplicationStartupPolicy.ShouldCreateTrayService(trayAlreadyExists: false));
        Assert.IsFalse(ApplicationStartupPolicy.ShouldCreateTrayService(trayAlreadyExists: true));
    }
}
