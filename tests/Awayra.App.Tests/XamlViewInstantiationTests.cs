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
}
