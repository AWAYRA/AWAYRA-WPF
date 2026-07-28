namespace Awayra.App.Services;

public sealed class ExternalLinkLaunchResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Url { get; init; }

    public static ExternalLinkLaunchResult Succeeded() => new() { Success = true };

    public static ExternalLinkLaunchResult Failed(string errorMessage, string url) =>
        new() { Success = false, ErrorMessage = errorMessage, Url = url };
}

public interface IExternalLinkLauncher
{
    ExternalLinkLaunchResult TryLaunch(string url);
}
