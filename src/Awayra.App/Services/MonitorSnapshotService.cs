using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Awayra.App.Interop;
using Awayra.Core.Abstractions;

namespace Awayra.App.Services;

public sealed class MonitorSnapshotService : IMonitorSnapshotService
{
    private readonly IAppLogger _logger;

    public MonitorSnapshotService(IAppLogger logger) => _logger = logger;

    public ImageSource? CaptureMonitorAtCursor()
    {
        try
        {
            var screen = MonitorLocator.GetCursorScreen();
            var bounds = screen.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

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
        catch (Exception ex)
        {
            _logger.Warning($"Monitor snapshot capture failed: {ex.Message}");
            return null;
        }
    }
}
