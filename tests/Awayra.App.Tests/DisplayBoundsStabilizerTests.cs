using Awayra.App.Interop;

namespace Awayra.App.Tests;

[TestClass]
public sealed class DisplayBoundsStabilizerTests
{
    [TestMethod]
    public void Observe_RequiresTwoIdenticalSamplesByDefault()
    {
        var stabilizer = new DisplayBoundsStabilizer();
        var bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080);

        Assert.IsFalse(stabilizer.Observe(bounds));
        Assert.IsTrue(stabilizer.Observe(bounds));
        Assert.AreEqual(bounds, stabilizer.CurrentBounds);
    }

    [TestMethod]
    public void Observe_ChangedBoundsRestartTheStableSampleCount()
    {
        var stabilizer = new DisplayBoundsStabilizer();
        var first = new System.Drawing.Rectangle(0, 0, 1920, 1080);
        var second = new System.Drawing.Rectangle(0, 0, 2560, 1440);

        Assert.IsFalse(stabilizer.Observe(first));
        Assert.IsFalse(stabilizer.Observe(second));
        Assert.IsTrue(stabilizer.Observe(second));
        Assert.AreEqual(second, stabilizer.CurrentBounds);
    }

    [TestMethod]
    public void Observe_MaximumSamplesPreventsEndlessRecoveryDelay()
    {
        var stabilizer = new DisplayBoundsStabilizer(requiredStableSamples: 3, maximumSamples: 4);

        Assert.IsFalse(stabilizer.Observe(new System.Drawing.Rectangle(0, 0, 1920, 1080)));
        Assert.IsFalse(stabilizer.Observe(new System.Drawing.Rectangle(0, 0, 2560, 1440)));
        Assert.IsFalse(stabilizer.Observe(new System.Drawing.Rectangle(0, 0, 1920, 1080)));
        Assert.IsTrue(stabilizer.Observe(new System.Drawing.Rectangle(0, 0, 2560, 1440)));
    }

    [TestMethod]
    public void Reset_ClearsPriorDisplaySamples()
    {
        var stabilizer = new DisplayBoundsStabilizer();
        var bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080);

        Assert.IsFalse(stabilizer.Observe(bounds));
        stabilizer.Reset();

        Assert.IsNull(stabilizer.CurrentBounds);
        Assert.IsFalse(stabilizer.Observe(bounds));
    }
}
