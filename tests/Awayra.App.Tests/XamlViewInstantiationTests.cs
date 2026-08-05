using System.Windows;
using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.App.Views;
using Awayra.Core.Models;
using Awayra.Core.Persistence;

namespace Awayra.App.Tests;

[TestClass]
public sealed class XamlViewInstantiationTests
{
    [TestMethod]
    public void MainWindow_InstantiatesWithoutXamlParseException()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var window = new MainWindow(new MainViewModel(host, () => { }));
            Assert.IsNotNull(window);
            window.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void SettingsWindow_InstantiatesWithoutXamlParseException()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var owner = new MainWindow(new MainViewModel(host, () => { }));
            owner.Show();
            SettingsWindow? settingsWindow = null;
            settingsWindow = new SettingsWindow(new SettingsViewModel(host, _ => settingsWindow?.Close()))
            {
                Owner = owner
            };
            Assert.IsNotNull(settingsWindow);
            settingsWindow.Close();
            owner.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void AboutWindow_InstantiatesWithoutXamlParseException()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            AboutWindow? aboutWindow = null;
            aboutWindow = new AboutWindow(new AboutViewModel(new FakeExternalLinkLauncher(), () => aboutWindow?.Close()));
            Assert.IsNotNull(aboutWindow);
            aboutWindow.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void BreakOverlayWindow_InstantiatesWithoutXamlParseException()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            Assert.IsNotNull(overlay);
            overlay.CloseSafely();
            host.Dispose();
        });
    }

    [TestMethod]
    public void BreakOverlayWindow_RespectsReducedMotionSetting()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            host.Settings.ReducedMotion = true;
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            overlay.Configure(
                new BreakStartedEventArgs
                {
                    BreakType = BreakType.Eye,
                    DurationSeconds = 20,
                    ActivityIndex = 0
                },
                host.Settings,
                host.Localization,
                isEye: true);

            Assert.IsTrue(overlay.DataContext is OverlayViewModel vm && vm.ReducedMotion);
            overlay.CloseSafely();
            host.Dispose();
        });
    }

    [TestMethod]
    public void EyeExerciseView_InstantiatesWithoutXamlParseException()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var view = new EyeExerciseView();
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.FindName("BlinkCounterText"));
            view.StartAnimation();
            view.StopAnimation();
        });
    }

    [TestMethod]
    public void MoveExerciseView_InstantiatesWithoutXamlParseException()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var view = new MoveExerciseView();
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.FindName("MoveCaptionText"));
            view.StartAnimation();
            view.StopAnimation();
        });
    }

    [TestMethod]
    public void EyeBreakOverlay_ShowsOnlyTheEyeExercise()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            overlay.Configure(
                new BreakStartedEventArgs { BreakType = BreakType.Eye, DurationSeconds = 20, ActivityIndex = 0 },
                host.Settings,
                host.Localization,
                isEye: true);

            var eye = (UIElement)overlay.FindName("EyeExercise");
            var move = (UIElement)overlay.FindName("MoveExercise");
            Assert.AreEqual(Visibility.Visible, eye.Visibility);
            Assert.AreEqual(Visibility.Collapsed, move.Visibility);

            overlay.CloseSafely();
            host.Dispose();
        });
    }

    [TestMethod]
    public void MoveBreakOverlay_ShowsOnlyTheMoveExercise()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            overlay.Configure(
                new BreakStartedEventArgs { BreakType = BreakType.Move, DurationSeconds = 60, ActivityIndex = 2 },
                host.Settings,
                host.Localization,
                isEye: false);

            var eye = (UIElement)overlay.FindName("EyeExercise");
            var move = (UIElement)overlay.FindName("MoveExercise");
            Assert.AreEqual(Visibility.Collapsed, eye.Visibility);
            Assert.AreEqual(Visibility.Visible, move.Visibility);

            overlay.CloseSafely();
            host.Dispose();
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DisablingTheBreakAnimation_HidesBothExercisesEntirely(bool isEye)
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            host.Settings.BreakAnimationEnabled = false;
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            overlay.Configure(
                new BreakStartedEventArgs
                {
                    BreakType = isEye ? BreakType.Eye : BreakType.Move,
                    DurationSeconds = 20,
                    ActivityIndex = 0
                },
                host.Settings,
                host.Localization,
                isEye: isEye);

            var eye = (UIElement)overlay.FindName("EyeExercise");
            var move = (UIElement)overlay.FindName("MoveExercise");
            Assert.AreEqual(Visibility.Collapsed, eye.Visibility);
            Assert.AreEqual(Visibility.Collapsed, move.Visibility);

            overlay.CloseSafely();
            host.Dispose();
        });
    }

    [TestMethod]
    public void ReducedMotion_ReplacesTheAnimatedEyeCueWithStaticGuidance()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            host.Settings.ReducedMotion = true;
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            overlay.Configure(
                new BreakStartedEventArgs { BreakType = BreakType.Eye, DurationSeconds = 20, ActivityIndex = 0 },
                host.Settings,
                host.Localization,
                isEye: true);

            var eye = (EyeExerciseView)overlay.FindName("EyeExercise");
            var counter = (System.Windows.Controls.TextBlock)eye.FindName("BlinkCounterText");

            // The animated per-blink counter must be replaced by a single static instruction.
            Assert.AreEqual("Then blink slowly ten times", counter.Text);

            overlay.CloseSafely();
            host.Dispose();
        });
    }

    [TestMethod]
    public void ReducedMotion_ReplacesTheAnimatedMoveCaptionWithStaticGuidance()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            host.Settings.ReducedMotion = true;
            var overlay = new BreakOverlayWindow(host, new OverlayViewModel(), new NullMonitorSnapshotService());
            overlay.Configure(
                new BreakStartedEventArgs { BreakType = BreakType.Move, DurationSeconds = 60, ActivityIndex = 1 },
                host.Settings,
                host.Localization,
                isEye: false);

            var move = (MoveExerciseView)overlay.FindName("MoveExercise");
            var caption = (System.Windows.Controls.TextBlock)move.FindName("MoveCaptionText");

            Assert.AreEqual("Stand up, walk, stretch, three squats, three jumps, then sit back down", caption.Text);

            overlay.CloseSafely();
            host.Dispose();
        });
    }
}
