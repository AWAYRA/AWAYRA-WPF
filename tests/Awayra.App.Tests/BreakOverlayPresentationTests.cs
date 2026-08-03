using System.Windows.Threading;
using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.App.Views;
using Awayra.Core.Models;

namespace Awayra.App.Tests;

[TestClass]
public sealed class BreakOverlayPresentationTests
{
    [TestMethod]
    public void ShowOnActiveMonitor_RevealsAfterFirstRenderedFrame()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var window = new BreakOverlayWindow(
                host,
                new OverlayViewModel(),
                new NullMonitorSnapshotService());

            window.Configure(
                new BreakStartedEventArgs
                {
                    BreakType = BreakType.Eye,
                    DurationSeconds = 20,
                    ActivityIndex = 0
                },
                AppSettings.CreateDefault(),
                new LocalizationService(),
                isEye: true);

            window.ShowOnActiveMonitor();
            PumpDispatcherUntilIdle(window.Dispatcher);

            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(1d, window.Opacity, 0.001d);
            Assert.IsTrue(window.ActualWidth > 0);
            Assert.IsTrue(window.ActualHeight > 0);

            window.CloseSafely();
            host.Dispose();
        });
    }

    private static void PumpDispatcherUntilIdle(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
