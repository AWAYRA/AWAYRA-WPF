using Awayra.App.Services;

namespace Awayra.App.Tests;

[TestClass]
public sealed class StartupExperienceTests
{
    [TestMethod]
    public void FreshInstall_ShowsOnboarding()
    {
        Assert.IsTrue(OnboardingPolicy.ShouldShow(
            isUiTestMode: false,
            settingsFileExists: false,
            markerFileExists: false));
    }

    [TestMethod]
    public void InstallerMarker_ShowsOnboardingForExistingSettings()
    {
        Assert.IsTrue(OnboardingPolicy.ShouldShow(
            isUiTestMode: false,
            settingsFileExists: true,
            markerFileExists: true));
    }

    [TestMethod]
    public void NormalLaunch_DoesNotRepeatOnboarding()
    {
        Assert.IsFalse(OnboardingPolicy.ShouldShow(
            isUiTestMode: false,
            settingsFileExists: true,
            markerFileExists: false));
    }

    [TestMethod]
    public void UiTestMode_NeverShowsOnboarding()
    {
        Assert.IsFalse(OnboardingPolicy.ShouldShow(
            isUiTestMode: true,
            settingsFileExists: false,
            markerFileExists: true));
    }

    [TestMethod]
    public void BootTimeProvider_ReturnsPlausibleCurrentBootTime()
    {
        var before = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        var actual = SystemBootTimeProvider.GetBootStartedAtUtc();
        var after = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);

        Assert.IsTrue(actual >= before - TimeSpan.FromSeconds(1));
        Assert.IsTrue(actual <= after + TimeSpan.FromSeconds(1));
    }
}
