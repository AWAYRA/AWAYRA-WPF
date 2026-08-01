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
            var eyeArgs = CreateArgs(BreakType.Eye, 20);
            var moveArgs = CreateArgs(BreakType.Move, 60);

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
    public void ShowBreak_DuplicateVisibleRequest_DoesNotRecreateOverlay()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var eyeFactoryCalls = 0;
            var coordinator = new OverlayCoordinator(
                () =>
                {
                    eyeFactoryCalls++;
                    return new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
                },
                () => new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService()),
                new NullLogger());

            var settings = AppSettings.CreateDefault();
            var localization = new LocalizationService();
            var eyeArgs = CreateArgs(BreakType.Eye, 20);

            coordinator.ShowBreak(eyeArgs, settings, localization);
            coordinator.ShowBreak(eyeArgs, settings, localization);

            Assert.AreEqual(1, eyeFactoryCalls);
            Assert.IsTrue(coordinator.SessionState.EyeVisible);
            Assert.IsFalse(coordinator.SessionState.BothVisible);

            coordinator.CloseAll();
            host.Dispose();
        });
    }

    private static BreakStartedEventArgs CreateArgs(BreakType type, int durationSeconds) => new()
    {
        BreakType = type,
        DurationSeconds = durationSeconds,
        ActivityIndex = 0
    };
}
