using Awayra.Core.Abstractions;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class SingleInstanceCoordinatorTests
{
    private static bool _globalAcquired;

    [TestInitialize]
    public void Setup() => _globalAcquired = false;

    [TestMethod]
    public void TryAcquire_SecondCallFailsUntilReleased()
    {
        var first = new TestSingleInstanceCoordinator();
        var second = new TestSingleInstanceCoordinator();

        Assert.IsTrue(first.TryAcquire());
        Assert.IsFalse(second.TryAcquire());

        first.Release();
        Assert.IsTrue(second.TryAcquire());
    }

    [TestMethod]
    public void Signal_InvokesListener()
    {
        var coordinator = new TestSingleInstanceCoordinator();
        var signaled = false;
        coordinator.ListenForSignals(() => signaled = true);

        coordinator.SignalExistingInstance();

        Assert.IsTrue(signaled);
    }

    private sealed class TestSingleInstanceCoordinator : ISingleInstanceCoordinator
    {
        private Action? _listener;

        public bool TryAcquire()
        {
            if (_globalAcquired)
            {
                return false;
            }

            _globalAcquired = true;
            return true;
        }

        public void SignalExistingInstance() => _listener?.Invoke();

        public void ListenForSignals(Action onSignal) => _listener = onSignal;

        public void Release() => _globalAcquired = false;
    }
}
