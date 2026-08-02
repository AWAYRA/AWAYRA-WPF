namespace Awayra.App.Services;

public static class AppLinkUrls
{
    public const string Source = "https://github.com/AWAYRA/AWAYRA-WPF";
    public const string Issues = "https://github.com/AWAYRA/AWAYRA-WPF/issues";
    public const string Support = "";

    public static bool IsSupportConfigured =>
        Uri.TryCreate(Support, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}