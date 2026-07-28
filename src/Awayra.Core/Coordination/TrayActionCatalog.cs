namespace Awayra.Core.Coordination;

public enum TrayUserAction
{
    OpenDashboard,
    EyeResetNow,
    MoveBreakNow,
    TogglePause,
    OpenSettings,
    Quit
}

public static class TrayActionCatalog
{
    public static IReadOnlyList<TrayUserAction> MenuActionsInOrder { get; } =
    [
        TrayUserAction.OpenDashboard,
        TrayUserAction.EyeResetNow,
        TrayUserAction.MoveBreakNow,
        TrayUserAction.TogglePause,
        TrayUserAction.OpenSettings,
        TrayUserAction.Quit
    ];

    public static TrayUserAction LeftClickAction => TrayUserAction.OpenDashboard;

    public static TrayUserAction DoubleClickAction => TrayUserAction.OpenDashboard;

    public static bool RequestsDashboardRestore(TrayUserAction action) =>
        action is TrayUserAction.OpenDashboard;

    public static bool RequestsSettings(TrayUserAction action) =>
        action is TrayUserAction.OpenSettings;

    public static bool RequestsEyeOverlay(TrayUserAction action) =>
        action is TrayUserAction.EyeResetNow;

    public static bool RequestsMoveOverlay(TrayUserAction action) =>
        action is TrayUserAction.MoveBreakNow;

    public static bool RequestsShutdown(TrayUserAction action) =>
        action is TrayUserAction.Quit;
}
