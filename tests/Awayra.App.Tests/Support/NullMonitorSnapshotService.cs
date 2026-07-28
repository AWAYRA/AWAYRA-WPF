using Awayra.App.Services;

namespace Awayra.App.Tests.Support;

public sealed class NullMonitorSnapshotService : IMonitorSnapshotService
{
    public System.Windows.Media.ImageSource? CaptureMonitorAtCursor() => null;
}
