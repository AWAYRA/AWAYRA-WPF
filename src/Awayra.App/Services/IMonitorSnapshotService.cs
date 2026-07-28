using System.Windows.Media;

namespace Awayra.App.Services;

public interface IMonitorSnapshotService
{
    ImageSource? CaptureMonitorAtCursor();
}
