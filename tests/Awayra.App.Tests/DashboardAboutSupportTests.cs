using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.App.Views;
using Awayra.Core.Models;

namespace Awayra.App.Tests;

[TestClass]
public sealed class DashboardAboutSupportTests
{
    [TestMethod]
    public void Dashboard_ShowsAboutSupportButtonSpanningActionWidth()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var dashboard = new MainWindow(new MainViewModel(host, () => { }));
            dashboard.Show();
            dashboard.UpdateLayout();

            var button = FindElementByAutomationId(dashboard, "DashboardAboutSupportButton") as Button
                ?? throw new InvalidOperationException("DashboardAboutSupportButton not found.");
            Assert.IsTrue(button.ActualWidth >= dashboard.ActualWidth - 80);
            Assert.IsNotNull(FindTextBlockContaining(dashboard, "About & Support Awayra"));
            Assert.IsNotNull(FindTextBlockContaining(dashboard, "Built with love for people who spend long hours at a computer."));

            dashboard.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void DashboardAboutSupportButton_OpensSingleAboutWindow()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var dashboard = new MainWindow(new MainViewModel(host, () => { }));
            dashboard.Show();

            ClickDashboardAboutButton(dashboard);
            Assert.AreEqual(1, CountVisibleAboutWindows());

            ClickDashboardAboutButton(dashboard);
            Assert.AreEqual(1, CountVisibleAboutWindows());

            CloseVisibleAboutWindows();
            dashboard.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void AboutOpen_FromDashboard_DoesNotChangeSchedulerSnapshot()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var before = host.Scheduler.GetSnapshot();
            var dashboard = new MainWindow(new MainViewModel(host, () => { }));
            dashboard.Show();

            ClickDashboardAboutButton(dashboard);
            CloseVisibleAboutWindows();

            var after = host.Scheduler.GetSnapshot();
            Assert.AreEqual(before.Status, after.Status);
            Assert.IsTrue(Math.Abs((before.EyeRemaining - after.EyeRemaining).TotalSeconds) <= 2);
            Assert.IsTrue(Math.Abs((before.MoveRemaining - after.MoveRemaining).TotalSeconds) <= 2);
            Assert.AreEqual(before.IsPausedManual, after.IsPausedManual);

            dashboard.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void AboutViewModel_SupportDisabledWhenNoUrlIsConfigured()
    {
        var vm = new AboutViewModel(new FakeExternalLinkLauncher(), () => { });
        Assert.IsFalse(vm.IsSupportConfigured);
        Assert.IsTrue(vm.ShowSupportUnavailable);
        Assert.AreEqual("Support link is not configured yet.", vm.SupportUnavailableMessage);
        Assert.IsFalse(vm.OpenSupportCommand.CanExecute(null));
        Assert.AreEqual(string.Empty, AppLinkUrls.Support);
    }

    [TestMethod]
    public void AboutViewModel_RepositoryUrlsUseCurrentOrganization()
    {
        Assert.AreEqual("https://github.com/AWAYRA/AWAYRA-WPF", AppLinkUrls.Source);
        Assert.AreEqual("https://github.com/AWAYRA/AWAYRA-WPF/issues", AppLinkUrls.Issues);
    }

    [TestMethod]
    public void DashboardAboutSupport_CaptureAuditScreenshots()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var dashboard = new MainWindow(new MainViewModel(host, () => { }));
            dashboard.Show();
            dashboard.UpdateLayout();
            SaveWindowScreenshot(dashboard, "dashboard-about-support.png");

            ClickDashboardAboutButton(dashboard);
            var about = Application.Current.Windows.OfType<AboutWindow>().Single(window => window.IsVisible);
            about.UpdateLayout();
            SaveWindowScreenshot(about, "about-window-from-dashboard.png");
            about.Close();

            SettingsWindow? settingsWindow = null;
            settingsWindow = new SettingsWindow(new SettingsViewModel(host, _ => settingsWindow?.Close()))
            {
                Width = 980,
                Height = 720
            };
            settingsWindow.Show();
            settingsWindow.UpdateLayout();
            SaveWindowScreenshot(settingsWindow, "settings-no-scroll-980x720.png");

            settingsWindow.Width = 760;
            settingsWindow.Height = 580;
            settingsWindow.UpdateLayout();
            SaveWindowScreenshot(settingsWindow, "settings-small-window-scroll.png");

            settingsWindow.Close();
            dashboard.Close();
            host.Dispose();
        });
    }

    private static void ClickDashboardAboutButton(MainWindow dashboard)
    {
        var button = FindElementByAutomationId(dashboard, "DashboardAboutSupportButton") as Button
            ?? throw new InvalidOperationException("DashboardAboutSupportButton not found.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private static int CountVisibleAboutWindows() =>
        Application.Current.Windows.OfType<AboutWindow>().Count(window => window.IsVisible);

    private static void CloseVisibleAboutWindows()
    {
        foreach (var window in Application.Current.Windows.OfType<AboutWindow>().ToArray())
        {
            window.Close();
        }
    }

    private static FrameworkElement? FindElementByAutomationId(DependencyObject root, string automationId)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element &&
                string.Equals(AutomationProperties.GetAutomationId(element), automationId, StringComparison.Ordinal))
            {
                return element;
            }

            var match = FindElementByAutomationId(child, automationId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static TextBlock? FindTextBlockContaining(DependencyObject root, string text)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock textBlock &&
                string.Equals(textBlock.Text, text, StringComparison.Ordinal))
            {
                return textBlock;
            }

            var match = FindTextBlockContaining(child, text);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static void SaveWindowScreenshot(Window window, string fileName)
    {
        window.UpdateLayout();
        var width = Math.Max(1, (int)window.ActualWidth);
        var height = Math.Max(1, (int)window.ActualHeight);
        var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        render.Render(window);

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var outputDir = Path.Combine(repoRoot, "artifacts", "ui-audit");
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Assert.IsTrue(File.Exists(path), $"Expected screenshot at {path}");
    }
}