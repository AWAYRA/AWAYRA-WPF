namespace Awayra.Core.Coordination;

public readonly record struct ScreenBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public bool Intersects(ScreenBounds other) =>
        Left < other.Right &&
        Right > other.Left &&
        Top < other.Bottom &&
        Bottom > other.Top;
}

public static class OverlayLayoutCalculator
{
    public static ScreenBounds CenterWithinWorkingArea(ScreenBounds window, ScreenBounds workingArea)
    {
        var left = workingArea.Left + Math.Max(0, (workingArea.Width - window.Width) / 2);
        var top = workingArea.Top + Math.Max(0, (workingArea.Height - window.Height) / 2);
        return new ScreenBounds(left, top, window.Width, window.Height);
    }

    public static bool IsOnAnyWorkingArea(ScreenBounds window, IEnumerable<ScreenBounds> workingAreas) =>
        workingAreas.Any(area => area.Intersects(window));

    public static ScreenBounds SelectMonitorForPoint(double x, double y, IReadOnlyList<ScreenBounds> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("At least one monitor is required.");
        }

        foreach (var monitor in monitors)
        {
            if (x >= monitor.Left && x < monitor.Right && y >= monitor.Top && y < monitor.Bottom)
            {
                return monitor;
            }
        }

        return monitors
            .OrderBy(m => DistanceToCenter(x, y, m))
            .First();
    }

    private static double DistanceToCenter(double x, double y, ScreenBounds monitor)
    {
        var centerX = monitor.Left + monitor.Width / 2;
        var centerY = monitor.Top + monitor.Height / 2;
        var dx = x - centerX;
        var dy = y - centerY;
        return dx * dx + dy * dy;
    }
}
