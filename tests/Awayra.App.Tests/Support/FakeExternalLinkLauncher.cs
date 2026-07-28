using Awayra.App.Services;

namespace Awayra.App.Tests.Support;

internal sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
{
    public List<string> LaunchedUrls { get; } = [];

    public Func<string, ExternalLinkLaunchResult>? Handler { get; set; }

    public ExternalLinkLaunchResult TryLaunch(string url)
    {
        if (Handler is not null)
        {
            return Handler(url);
        }

        LaunchedUrls.Add(url);
        return ExternalLinkLaunchResult.Succeeded();
    }
}
