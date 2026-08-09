using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Awayra.App.Interop;

namespace Awayra.App.Services;

public static class AppIconHelper
{
    private static Icon? _applicationIcon;
    private static ImageSource? _applicationImageSource;

    public static Icon ApplicationIcon => _applicationIcon ??= LoadApplicationIcon();

    /// <summary>
    /// Built once and frozen. It used to allocate a fresh unfrozen BitmapSource, and leak a GDI
    /// bitmap handle through it, on every window that asked for an icon.
    /// </summary>
    public static ImageSource ApplicationImageSource =>
        _applicationImageSource ??= CreateImageSource(ApplicationIcon);

    public static void ApplyToWindow(Window window)
    {
        void Apply()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var iconHandle = ApplicationIcon.Handle;
            NativeMethods.SendMessage(handle, NativeMethods.WmSetIcon, NativeMethods.IconBig, iconHandle);
            NativeMethods.SendMessage(handle, NativeMethods.WmSetIcon, NativeMethods.IconSmall, iconHandle);
        }

        if (window.IsLoaded)
        {
            Apply();
            return;
        }

        window.SourceInitialized += (_, _) => Apply();
    }

    private static Icon LoadApplicationIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            using var embedded = Icon.ExtractAssociatedIcon(processPath);
            if (embedded is not null)
            {
                return (Icon)embedded.Clone();
            }
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "awayra.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return SystemIcons.Application;
    }

    private static BitmapSource CreateImageSource(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(handle);
        }
    }
}
