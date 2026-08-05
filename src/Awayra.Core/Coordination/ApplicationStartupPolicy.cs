using Awayra.Core.Models;

namespace Awayra.Core.Coordination;

public static class ApplicationStartupPolicy
{
    public static bool ShouldShowDashboardOnStartup(AppSettings settings) =>
        !settings.StartMinimized;

    public static bool ShouldHideDashboardToTrayOnClose(AppSettings settings, bool isQuitting) =>
        !isQuitting && settings.CloseToTray;
}
