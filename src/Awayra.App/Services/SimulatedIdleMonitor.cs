using Awayra.Core.Abstractions;

namespace Awayra.App.Services;

public sealed class SimulatedIdleMonitor : IIdleMonitor
{
    private readonly IIdleMonitor _inner;
    private bool? _simulatedIdle;

    public SimulatedIdleMonitor(IIdleMonitor inner) => _inner = inner;

    public void SetSimulatedIdle(bool? isIdle) => _simulatedIdle = isIdle;

    public TimeSpan GetIdleTime()
    {
        if (_simulatedIdle == true)
        {
            return TimeSpan.FromHours(1);
        }

        if (_simulatedIdle == false)
        {
            return TimeSpan.Zero;
        }

        return _inner.GetIdleTime();
    }

    public bool IsIdle(TimeSpan threshold) => GetIdleTime() >= threshold;
}
