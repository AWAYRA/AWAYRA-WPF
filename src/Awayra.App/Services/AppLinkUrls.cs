namespace Awayra.App.Services;

public static class AppLinkUrls
{
    public const string Source = "https://github.com/mtalavi/Awayra";
    public const string Issues = "https://github.com/mtalavi/Awayra/issues";
    public const string Support = "https://www.buymeacoffee.com/YOUR_USERNAME";

    public static bool IsSupportConfigured =>
        !string.IsNullOrWhiteSpace(Support) &&
        !Support.Contains("YOUR_USERNAME", StringComparison.OrdinalIgnoreCase);
}
