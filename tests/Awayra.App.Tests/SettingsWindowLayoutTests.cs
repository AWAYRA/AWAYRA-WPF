using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.App.Views;

namespace Awayra.App.Tests;

[TestClass]
public sealed class SettingsWindowLayoutTests
{
    [TestMethod]
    public void SettingsWindow_At980x720_HasNoVerticalScrollbar()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            SettingsWindow? settingsWindow = null;
            settingsWindow = new SettingsWindow(new SettingsViewModel(host, _ => settingsWindow?.Close()))
            {
                Width = 980,
                Height = 720
            };
            settingsWindow.Show();
            settingsWindow.UpdateLayout();

            var scrollViewer = FindScrollViewer(settingsWindow)
                ?? throw new InvalidOperationException("Settings ScrollViewer not found.");
            Assert.AreNotEqual(
                Visibility.Visible,
                scrollViewer.ComputedVerticalScrollBarVisibility,
                $"Vertical overflow: extent={scrollViewer.ExtentHeight:F1}, viewport={scrollViewer.ViewportHeight:F1}, actual={scrollViewer.ActualHeight:F1}.");
            Assert.AreNotEqual(Visibility.Visible, scrollViewer.ComputedHorizontalScrollBarVisibility);
            Assert.IsNotNull(FindElementByAutomationId(settingsWindow, "AllowSnoozeCheckbox"));
            Assert.IsNotNull(FindElementByAutomationId(settingsWindow, "GlassClarityInput"));
            Assert.IsNotNull(FindElementByAutomationId(settingsWindow, "SettingsSaveButton"));

            settingsWindow.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void SettingsWindow_At760x580_AllowsVerticalScroll()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            SettingsWindow? settingsWindow = null;
            settingsWindow = new SettingsWindow(new SettingsViewModel(host, _ => settingsWindow?.Close()))
            {
                Width = 760,
                Height = 580
            };
            settingsWindow.Show();
            settingsWindow.UpdateLayout();

            var scrollViewer = FindScrollViewer(settingsWindow)
                ?? throw new InvalidOperationException("Settings ScrollViewer not found.");
            Assert.AreEqual(Visibility.Visible, scrollViewer.ComputedVerticalScrollBarVisibility);
            Assert.AreNotEqual(Visibility.Visible, scrollViewer.ComputedHorizontalScrollBarVisibility);
            Assert.IsNotNull(FindElementByAutomationId(settingsWindow, "SettingsSaveButton"));

            settingsWindow.Close();
            host.Dispose();
        });
    }

    [TestMethod]
    public void SettingsWindow_DoesNotContainAboutEntry()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            SettingsWindow? settingsWindow = null;
            settingsWindow = new SettingsWindow(new SettingsViewModel(host, _ => settingsWindow?.Close()));
            settingsWindow.Show();
            settingsWindow.UpdateLayout();

            Assert.IsNull(FindElementByAutomationId(settingsWindow, "AboutAwayraButton"));

            settingsWindow.Close();
            host.Dispose();
        });
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            var match = FindScrollViewer(child);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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
}
