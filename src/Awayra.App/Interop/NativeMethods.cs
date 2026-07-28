using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;
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
