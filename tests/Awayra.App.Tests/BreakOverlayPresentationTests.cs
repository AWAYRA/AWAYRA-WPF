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
    public void ShowOnActiveMonitor_RevealsAfterPostPositionRenderFrames()
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
            PumpUntil(
                window.Dispatcher,
                () => Math.Abs(window.Opacity - 1d) < 0.001d,
                TimeSpan.FromSeconds(5));

            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(1d, window.Opacity, 0.001d);
            Assert.IsTrue(window.ActualWidth > 0);
            Assert.IsTrue(window.ActualHeight > 0);

            window.CloseSafely();
            host.Dispose();
        });
    }

    private static void PumpUntil(Dispatcher dispatcher, Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        Assert.IsTrue(condition(), "Overlay did not reveal after the required post-position render frames.");
    }
}
