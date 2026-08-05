using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace Awayra.App.Interop;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr hObject);

    internal const int SwRestore = 9;
    internal const int WmSetIcon = 0x0080;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;
    internal static readonly IntPtr HwndTopmost = new(-1);
    internal static readonly IntPtr IconSmall = IntPtr.Zero;
    internal static readonly IntPtr IconBig = new(1);
}

public sealed class MonitorLocator
{
    public static Screen GetCursorScreen()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return Screen.PrimaryScreen
                ?? Screen.AllScreens.FirstOrDefault()
                ?? throw new InvalidOperationException("No display available.");
        }

        return Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
    }

    public static bool PositionWindowOnCursorMonitor(Window window, bool force = false) =>
        PositionWindowOnBounds(window, GetCursorScreen().Bounds, force);

    public static bool PositionWindowOnBounds(
        Window window,
        System.Drawing.Rectangle bounds,
        bool force = false)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            if (!force && IsWindowAtPhysicalBounds(handle, bounds))
            {
                return false;
            }

            var flags = NativeMethods.SwpNoActivate;
            if (window.IsVisible)
            {
                flags |= NativeMethods.SwpShowWindow;
            }

            if (NativeMethods.SetWindowPos(
                    handle,
                    NativeMethods.HwndTopmost,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    flags))
            {
                return true;
            }
        }

        return SetDipBoundsIfChanged(window, bounds, force);
    }

    public static bool IsWindowAtPhysicalBounds(
        Window window,
        System.Drawing.Rectangle bounds,
        int tolerance = 1)
    {
        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        return handle != IntPtr.Zero && IsWindowAtPhysicalBounds(handle, bounds, tolerance);
    }

    public static void EnsureWindowOnScreen(Window window)
    {
        if (window.WindowState == WindowState.Maximized)
        {
            return;
        }

        var width = window.Width > 0 ? window.Width : window.ActualWidth > 0 ? window.ActualWidth : window.MinWidth;
        var height = window.Height > 0 ? window.Height : window.ActualHeight > 0 ? window.ActualHeight : window.MinHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var left = double.IsNaN(window.Left) ? 0 : window.Left;
        var top = double.IsNaN(window.Top) ? 0 : window.Top;
        var windowRect = new System.Drawing.Rectangle((int)left, (int)top, (int)width, (int)height);

        var onScreen = Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowRect));
        if (onScreen)
        {
            return;
        }

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2);
        window.Top = workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2);
    }

    public static void ActivateWindow(Window window)
    {
        window.Visibility = Visibility.Visible;
        window.ShowInTaskbar = true;
        window.WindowState = WindowState.Normal;
        window.Show();
        window.Activate();

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(handle);
        }

        if (!window.IsActive)
        {
            window.Topmost = true;
            window.Activate();
            window.Topmost = false;
        }
    }

    private static bool IsWindowAtPhysicalBounds(
        IntPtr handle,
        System.Drawing.Rectangle bounds,
        int tolerance = 1)
    {
        if (!NativeMethods.GetWindowRect(handle, out var rect))
        {
            return false;
        }

        return Math.Abs(rect.Left - bounds.Left) <= tolerance
            && Math.Abs(rect.Top - bounds.Top) <= tolerance
            && Math.Abs((rect.Right - rect.Left) - bounds.Width) <= tolerance
            && Math.Abs((rect.Bottom - rect.Top) - bounds.Height) <= tolerance;
    }

    private static bool SetDipBoundsIfChanged(
        Window window,
        System.Drawing.Rectangle bounds,
        bool force)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1d;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1d;
        var left = bounds.Left / scaleX;
        var top = bounds.Top / scaleY;
        var width = bounds.Width / scaleX;
        var height = bounds.Height / scaleY;

        if (!force
            && NearlyEqual(window.Left, left)
            && NearlyEqual(window.Top, top)
            && NearlyEqual(window.Width, width)
            && NearlyEqual(window.Height, height))
        {
            return false;
        }

        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;
        return true;
    }

    private static bool NearlyEqual(double current, double target) =>
        !double.IsNaN(current) && Math.Abs(current - target) < 0.5d;
}
