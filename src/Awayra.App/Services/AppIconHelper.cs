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

    public static Icon ApplicationIcon => _applicationIcon ??= LoadApplicationIcon();

    public static ImageSource ApplicationImageSource => CreateImageSource(ApplicationIcon);

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
            var embedded = Icon.ExtractAssociatedIcon(processPath);
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

    private static ImageSource CreateImageSource(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        var handle = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(handle);
        }
    }
}
