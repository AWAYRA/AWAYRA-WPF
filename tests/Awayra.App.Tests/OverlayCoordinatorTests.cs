using System.Windows;
using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.App.Views;
using Awayra.Core.Models;
using Awayra.Core.Persistence;

namespace Awayra.App.Tests;

[TestClass]
public sealed class OverlayCoordinatorTests
{
    [TestMethod]
    public void ShowBreak_ExclusiveOverlaySession()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var coordinator = new OverlayCoordinator(
                () => new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService()),
                () => new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService()),
                new NullLogger());

            var settings = AppSettings.CreateDefault();
            var localization = new LocalizationService();
            var eyeArgs = new BreakStartedEventArgs
            {
                BreakType = BreakType.Eye,
                DurationSeconds = 20,
                ActivityIndex = 0
            };
            var moveArgs = new BreakStartedEventArgs
            {
                BreakType = BreakType.Move,
                DurationSeconds = 60,
                ActivityIndex = 0
            };

            coordinator.ShowBreak(eyeArgs, settings, localization);
            Assert.IsTrue(coordinator.SessionState.EyeVisible);
            Assert.IsFalse(coordinator.SessionState.MoveVisible);

            coordinator.ShowBreak(moveArgs, settings, localization);
            Assert.IsFalse(coordinator.SessionState.EyeVisible);
            Assert.IsTrue(coordinator.SessionState.MoveVisible);
            Assert.IsFalse(coordinator.SessionState.BothVisible);

            coordinator.CloseAll();
            Assert.IsFalse(coordinator.SessionState.HasAnyVisible);
            host.Dispose();
        });
    }

    [TestMethod]
    public void RecoverActiveOverlay_RecreatesClosedWindowAndPreservesBreakSession()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            BreakOverlayWindow? firstEyeWindow = null;
            BreakOverlayWindow? latestEyeWindow = null;
            var eyeWindowCount = 0;

            var coordinator = new OverlayCoordinator(
                () =>
                {
                    var window = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
                    eyeWindowCount++;
                    firstEyeWindow ??= window;
                    latestEyeWindow = window;
                    return window;
                },
                () => new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService()),
                new NullLogger());

            coordinator.ShowBreak(
                new BreakStartedEventArgs
                {
                    BreakType = BreakType.Eye,
                    DurationSeconds = 20,
                    ActivityIndex = 0
                },
                AppSettings.CreateDefault(),
                new LocalizationService());

            Assert.AreEqual(1, eyeWindowCount);
            Assert.IsNotNull(firstEyeWindow);
            firstEyeWindow!.CloseSafely();

            var recovered = coordinator.RecoverActiveOverlay();

            Assert.IsTrue(recovered);
            Assert.AreEqual(2, eyeWindowCount);
            Assert.IsNotNull(latestEyeWindow);
            Assert.AreNotSame(firstEyeWindow, latestEyeWindow);
            Assert.IsTrue(latestEyeWindow!.IsVisible);
            Assert.IsTrue(coordinator.SessionState.EyeVisible);
            Assert.IsFalse(coordinator.SessionState.MoveVisible);

            coordinator.CloseAll();
            host.Dispose();
        });
    }
}
