namespace Awayra.App.Services;

public static class SystemBootTimeProvider
{
    public static DateTimeOffset GetBootStartedAtUtc() =>
        DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
}

public static class OnboardingPolicy
{
    public static bool ShouldShow(bool isUiTestMode, bool settingsFileExists, bool markerFileExists) =>
        !isUiTestMode && (!settingsFileExists || markerFileExists);
}
