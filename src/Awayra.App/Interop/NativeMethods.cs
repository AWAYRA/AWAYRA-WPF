using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace Awayra.App.Interop;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    internal const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    internal const int SwRestore = 9;
}

public sealed class MonitorLocator
{
    public static Screen GetCursorScreen()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return Screen.PrimaryScreen ?? throw new InvalidOperationException("No display available.");
        }

        var handle = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
        foreach (var screen in Screen.AllScreens)
        {
            if ((IntPtr)screen.GetHashCode() == handle || screen.Bounds.Contains(point.X, point.Y))
            {
                return screen;
            }
        }

        return Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
    }

    public static void PositionWindowOnCursorMonitor(Window window)
    {
        var screen = GetCursorScreen();
        var bounds = screen.Bounds;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
    }

    public static void ActivateWindow(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(handle);
        }
    }
}

public static class DwmHelper
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void TryApplyBackdrop(Window window, bool darkMode)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var dark = darkMode ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

            var backdrop = 3; // Acrylic
            _ = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }
        catch
        {
            // Graceful fallback handled by XAML background.
        }
    }
}
