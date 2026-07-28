using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace Awayra.App.Tests.Support;

internal static class StaTestContext
{
    private static readonly object Sync = new();
    private static Dispatcher? _dispatcher;
    private static bool _initialized;

    private static void Ensure()
    {
        if (_initialized)
        {
            return;
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            var ready = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                ResolveEventHandler? resolver = null;
                resolver = (_, args) =>
                {
                    if (args.Name.StartsWith("Awayra.App,", StringComparison.Ordinal))
                    {
                        var path = Path.Combine(AppContext.BaseDirectory, "Awayra.dll");
                        if (File.Exists(path))
                        {
                            return Assembly.LoadFrom(path);
                        }
                    }

                    return null;
                };

                AppDomain.CurrentDomain.AssemblyResolve += resolver;
                _ = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                _dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "Awayra.WpfTests"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            _initialized = true;
        }
    }

    public static void Run(Action action)
    {
        Ensure();
        Exception? captured = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is not null)
        {
            throw captured;
        }
    }
}
