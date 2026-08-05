namespace Awayra.Core.Coordination;

public enum DashboardPresentation
{
    NotCreated,
    Hidden,
    Minimized,
    Visible
}

public readonly record struct DashboardRestorePlan(
    bool CreateIfMissing,
    bool Show,
    bool RestoreFromMinimized,
    bool Activate,
    bool EnsureOnScreen,
    bool ShowInTaskbar);

public static class DashboardRestorePlanner
{
    public static DashboardPresentation Classify(bool exists, bool isVisible, bool isMinimized)
    {
        if (!exists)
        {
            return DashboardPresentation.NotCreated;
        }

        if (isMinimized)
        {
            return DashboardPresentation.Minimized;
        }

        return isVisible ? DashboardPresentation.Visible : DashboardPresentation.Hidden;
    }

    public static DashboardRestorePlan Plan(DashboardPresentation presentation) =>
        presentation switch
        {
            DashboardPresentation.NotCreated => new(
                CreateIfMissing: true,
                Show: true,
                RestoreFromMinimized: true,
                Activate: true,
                EnsureOnScreen: true,
                ShowInTaskbar: true),
            DashboardPresentation.Hidden => new(
                CreateIfMissing: false,
                Show: true,
                RestoreFromMinimized: true,
                Activate: true,
                EnsureOnScreen: true,
                ShowInTaskbar: true),
            DashboardPresentation.Minimized => new(
                CreateIfMissing: false,
                Show: true,
                RestoreFromMinimized: true,
                Activate: true,
                EnsureOnScreen: true,
                ShowInTaskbar: true),
            DashboardPresentation.Visible => new(
                CreateIfMissing: false,
                Show: false,
                RestoreFromMinimized: true,
                Activate: true,
                EnsureOnScreen: true,
                ShowInTaskbar: true),
            _ => throw new ArgumentOutOfRangeException(nameof(presentation), presentation, null)
        };
}
