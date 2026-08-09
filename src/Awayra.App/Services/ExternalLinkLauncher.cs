using System.Diagnostics;

namespace Awayra.App.Services;

public sealed class ExternalLinkLauncher : IExternalLinkLauncher
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "www.buymeacoffee.com",
        "buymeacoffee.com"
    };

    public ExternalLinkLaunchResult TryLaunch(string url)
    {
        if (!TryValidateUrl(url, out var uri, out var errorMessage))
        {
            return ExternalLinkLaunchResult.Failed(errorMessage, url);
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });

            return ExternalLinkLaunchResult.Succeeded();
        }
        catch (Exception ex)
        {
            return ExternalLinkLaunchResult.Failed(ex.Message, uri.AbsoluteUri);
        }
    }

    public static bool TryValidateUrl(string url, out Uri uri, out string errorMessage)
    {
        uri = null!;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            errorMessage = "The link address is empty.";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsedUri) || parsedUri is null)
        {
            errorMessage = "The link address is not valid.";
            uri = null!;
            return false;
        }

        uri = parsedUri;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Only secure HTTPS links are allowed.";
            return false;
        }

        if (!AllowedHosts.Contains(uri.Host))
        {
            errorMessage = "This link host is not allowed.";
            return false;
        }

        return true;
    }
}
