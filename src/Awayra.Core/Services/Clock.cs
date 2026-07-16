using Awayra.Core.Abstractions;

namespace Awayra.Core.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset Now => DateTimeOffset.Now;
}

public sealed class FakeClock : IClock
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset initial)
    {
        _now = initial;
    }

    public DateTimeOffset UtcNow => _now.ToUniversalTime();
    public DateTimeOffset Now => _now;

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    public void Set(DateTimeOffset value) => _now = value;
}
