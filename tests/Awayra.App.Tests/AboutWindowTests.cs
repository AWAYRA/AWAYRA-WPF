using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.App.Views;

namespace Awayra.App.Tests;

[TestClass]
public sealed class AboutWindowTests
{
    [TestMethod]
    public void AppVersionInfo_ReturnsVersionPrefix()
    {
        var version = AppVersionInfo.GetDisplayVersion();
        StringAssert.StartsWith(version, "Version ");
    }

    [TestMethod]
    public void AboutViewModel_ContainsMissionAndCreatorText()
    {
        var vm = new AboutViewModel(new FakeExternalLinkLauncher(), () => { });
        StringAssert.Contains(vm.Mission, "Awayra was created with love");
        StringAssert.Contains(vm.Creator, "Farzin Alavi");
        StringAssert.Contains(vm.OpenSourceStatement, "free and open-source");
        StringAssert.Contains(vm.SupportDescription, "completely optional");
    }

    [TestMethod]
    public void AboutViewModel_SupportButtonStaysDisabledWithoutConfiguredUrl()
    {
        Assert.AreEqual(string.Empty, AppLinkUrls.Support);
        var launcher = new FakeExternalLinkLauncher();
        var vm = new AboutViewModel(launcher, () => { });
        Assert.IsFalse(vm.OpenSupportCommand.CanExecute(null));
        Assert.IsTrue(vm.ShowSupportUnavailable);
    }

    [TestMethod]
    public void AboutViewModel_SourceButtonOpensExactUrl()
    {
        var launcher = new FakeExternalLinkLauncher();
        var vm = new AboutViewModel(launcher, () => { });
        vm.OpenSourceCommand.Execute(null);
        Assert.AreEqual(AppLinkUrls.Source, launcher.LaunchedUrls[0]);
    }

    [TestMethod]
    public void AboutViewModel_IssuesButtonOpensExactUrl()
    {
        var launcher = new FakeExternalLinkLauncher();
        var vm = new AboutViewModel(launcher, () => { });
        vm.OpenIssuesCommand.Execute(null);
        Assert.AreEqual(AppLinkUrls.Issues, launcher.LaunchedUrls[0]);
    }

    [TestMethod]
    public void AboutViewModel_LinkFailureDoesNotThrow()
    {
        var launcher = new FakeExternalLinkLauncher
        {
            Handler = url => ExternalLinkLaunchResult.Failed("Browser unavailable.", url)
        };
        var vm = new AboutViewModel(launcher, () => { });
        vm.OpenSourceCommand.Execute(null);
        Assert.IsTrue(vm.HasLinkError);
        Assert.AreEqual(AppLinkUrls.Source, vm.FailedUrl);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.LinkErrorMessage));
    }

    [TestMethod]
    public void AboutOpen_DoesNotChangeSchedulerSnapshot()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var beforeEyeNextDue = host.Scheduler.State.EyeNextDue;
            var beforeMoveNextDue = host.Scheduler.State.MoveNextDue;
            var beforeActiveBreak = host.Scheduler.State.ActiveBreak;
            var beforeQueuedBreak = host.Scheduler.State.QueuedBreak;
            var beforeBreakEndsAt = host.Scheduler.State.BreakEndsAt;
            var beforePaused = host.Scheduler.State.IsPausedManual;

            AboutWindow? window = null;
            window = new AboutWindow(new AboutViewModel(new FakeExternalLinkLauncher(), () => window?.Close()));
            window.Show();
            window.UpdateLayout();
            window.Close();

            Assert.AreEqual(beforeEyeNextDue, host.Scheduler.State.EyeNextDue);
            Assert.AreEqual(beforeMoveNextDue, host.Scheduler.State.MoveNextDue);
            Assert.AreEqual(beforeActiveBreak, host.Scheduler.State.ActiveBreak);
            Assert.AreEqual(beforeQueuedBreak, host.Scheduler.State.QueuedBreak);
            Assert.AreEqual(beforeBreakEndsAt, host.Scheduler.State.BreakEndsAt);
            Assert.AreEqual(beforePaused, host.Scheduler.State.IsPausedManual);
            host.Dispose();
        });
    }

    [TestMethod]
    public void AboutWindow_CloseCommandClosesWindow()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            AboutWindow? window = null;
            window = new AboutWindow(new AboutViewModel(new FakeExternalLinkLauncher(), () => window?.Close()));
            window.Show();
            Assert.IsTrue(window.IsVisible);
            if (window.DataContext is AboutViewModel vm)
            {
                vm.CloseAboutCommand.Execute(null);
            }

            Assert.IsFalse(window.IsVisible);
            host.Dispose();
        });
    }

    [TestMethod]
    public void AboutSupportError_CaptureAuditScreenshot()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var launcher = new FakeExternalLinkLauncher
            {
                Handler = url => ExternalLinkLaunchResult.Failed("Browser unavailable.", url)
            };

            AboutWindow? window = null;
            window = new AboutWindow(new AboutViewModel(launcher, () => window?.Close()));
            if (window.DataContext is AboutViewModel vm)
            {
                vm.OpenSourceCommand.Execute(null);
            }

            window.Show();
            window.UpdateLayout();
            SaveWindowScreenshot(window, "about-support-error.png");
            window.Close();
            host.Dispose();
        });
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
