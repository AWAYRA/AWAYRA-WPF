using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Linq;

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
    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
    internal const uint SwpShowWindow = 0x0040;
    internal static readonly IntPtr HwndTopmost = new(-1);
    internal static readonly IntPtr IconSmall = IntPtr.Zero;
    internal static readonly IntPtr IconBig = new(1);
}

public sealed class MonitorLocator
{
    public static Screen GetCursorScreen()
    {
        if (NativeMethods.GetCursorPos(out var point))
        {
            return Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
        }

        return Screen.PrimaryScreen
            ?? Screen.AllScreens.FirstOrDefault()
            ?? throw new InvalidOperationException("No display available.");
    }

    public static void PositionWindowOnCursorMonitor(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var bounds = GetCursorScreen().Bounds;
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero &&
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HwndTopmost,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoActivate |
                NativeMethods.SwpFrameChanged |
                NativeMethods.SwpShowWindow))
        {
            return;
        }

        SetDipBounds(window, bounds);
    }

    public static void EnsureWindowOnScreen(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowState == WindowState.Maximized)
        {
            return;
        }

        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out var nativeRect))
        {
            var width = Math.Max(1, nativeRect.Right - nativeRect.Left);
            var height = Math.Max(1, nativeRect.Bottom - nativeRect.Top);
            var windowRect = new System.Drawing.Rectangle(nativeRect.Left, nativeRect.Top, width, height);
            if (screens.Any(screen => screen.WorkingArea.IntersectsWith(windowRect)))
            {
                return;
            }

            var workingArea = (Screen.PrimaryScreen ?? screens[0]).WorkingArea;
            var left = workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2);
            var top = workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2);
            _ = NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                left,
                top,
                width,
                height,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
            return;
        }

        EnsureUncreatedWindowOnScreen(window, screens);
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

    private static void EnsureUncreatedWindowOnScreen(Window window, Screen[] screens)
    {
        var width = window.Width > 0
            ? window.Width
            : window.ActualWidth > 0
                ? window.ActualWidth
                : window.MinWidth;
        var height = window.Height > 0
            ? window.Height
            : window.ActualHeight > 0
                ? window.ActualHeight
                : window.MinHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
        var left = double.IsNaN(window.Left) ? 0 : window.Left;
        var top = double.IsNaN(window.Top) ? 0 : window.Top;
        var windowRect = new System.Drawing.Rectangle(
            (int)Math.Round(left * scaleX),
            (int)Math.Round(top * scaleY),
            Math.Max(1, (int)Math.Round(width * scaleX)),
            Math.Max(1, (int)Math.Round(height * scaleY)));

        if (screens.Any(screen => screen.WorkingArea.IntersectsWith(windowRect)))
        {
            return;
        }

        var workingArea = (Screen.PrimaryScreen ?? screens[0]).WorkingArea;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = (workingArea.Left + Math.Max(0, (workingArea.Width - windowRect.Width) / 2)) / scaleX;
        window.Top = (workingArea.Top + Math.Max(0, (workingArea.Height - windowRect.Height) / 2)) / scaleY;
    }

    private static void SetDipBounds(Window window, System.Drawing.Rectangle bounds)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
        window.Left = bounds.Left / scaleX;
        window.Top = bounds.Top / scaleY;
        window.Width = bounds.Width / scaleX;
        window.Height = bounds.Height / scaleY;
    }
}

public static class DwmHelper
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaMicaEffect = 1029;
    private const int DwmsbtMainWindow = 2;
    private const int DwmsbtTransientWindow = 3;
    private const int DwmsbtTabbedWindow = 4;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    public static void TryApplyOverlayGlass(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyOverlayGlass(window);
            return;
        }

        window.SourceInitialized += (_, _) => ApplyOverlayGlass(window);
    }

    public static bool ApplyOverlayGlass(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var dark = 1;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

            var mica = 1;
            _ = DwmSetWindowAttribute(handle, DwmwaMicaEffect, ref mica, sizeof(int));

            var backdropTypes = new[] { DwmsbtTabbedWindow, DwmsbtTransientWindow, DwmsbtMainWindow };
            foreach (var backdrop in backdropTypes)
            {
                var value = backdrop;
                if (DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref value, sizeof(int)) == 0)
                {
                    break;
                }
            }

            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            _ = DwmExtendFrameIntoClientArea(handle, ref margins);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
