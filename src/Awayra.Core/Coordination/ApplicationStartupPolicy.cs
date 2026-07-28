using Awayra.Core.Models;

namespace Awayra.Core.Coordination;

public static class ApplicationStartupPolicy
{
    public static bool ShouldShowDashboardOnStartup(AppSettings settings) =>
        !settings.StartMinimized;

    public static bool ShouldHideDashboardToTrayOnClose(AppSettings settings, bool isQuitting) =>
        !isQuitting && settings.CloseToTray;

    public static bool ShouldCreateTrayService(bool trayAlreadyExists) =>
        !trayAlreadyExists;

    public static bool ShouldCreateDashboard(bool dashboardAlreadyExists) =>
        !dashboardAlreadyExists;
}
