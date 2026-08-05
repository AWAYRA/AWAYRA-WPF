using System.Windows;
using Awayra.App.Converters;
using Awayra.App.Services;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.App.Tests.Support;

internal static class WpfTestHost
{
    public static void EnsureApplicationResources()
    {
        if (Application.Current is null)
        {
            new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }

        // Mirrors App.xaml: the only application-scoped resource is the converter. Views bring
        // their own scoped palettes.
        if (!Application.Current!.Resources.Contains("BoolToVisibility"))
        {
            Application.Current.Resources["BoolToVisibility"] = new BoolToVisibilityConverter();
        }
    }

    public static ApplicationHost CreateHost(AppSettings? storedSettings = null)
    {
        var settingsStore = new InMemorySettingsStore();
        if (storedSettings is not null)
        {
            settingsStore.SaveAsync(storedSettings).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        var host = new ApplicationHost(
            new NullLogger(),
            new SystemClock(),
            settingsStore,
            new InMemoryStateStore(),
            new InMemoryStatisticsStore(),
            new NullIdleMonitor(),
            new NullAutostartService(),
            new LocalizationService(),
            Application.Current?.Dispatcher,
            NullBreakSoundService.Instance);

        host.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return host;
    }

    private sealed class NullIdleMonitor : IIdleMonitor
    {
        public TimeSpan GetIdleTime() => TimeSpan.Zero;
        public bool IsIdle(TimeSpan threshold) => false;
    }

    private sealed class NullAutostartService : IAutostartService
    {
        public bool IsEnabled() => false;
        public void Enable(string executablePath) { }
        public void Disable() { }
        public void RepairIfStale(string executablePath) { }
    }
}
