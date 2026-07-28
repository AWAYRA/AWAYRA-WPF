using Awayra.App.Services;

namespace Awayra.App.Tests;

[TestClass]
public sealed class ExternalLinkLauncherTests
{
    [TestMethod]
    [DataRow("http://github.com/mtalavi/Awayra")]
    [DataRow("file:///C:/secret")]
    [DataRow("javascript:alert(1)")]
    [DataRow("data:text/html,hello")]
    public void TryValidateUrl_RejectsInvalidSchemes(string url)
    {
        var valid = ExternalLinkLauncher.TryValidateUrl(url, out _, out var error);
        Assert.IsFalse(valid);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    [TestMethod]
    public void TryValidateUrl_RejectsUnknownHost()
    {
        var valid = ExternalLinkLauncher.TryValidateUrl("https://example.com/page", out _, out var error);
        Assert.IsFalse(valid);
        Assert.Contains("host", error, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow("https://github.com/mtalavi/Awayra")]
    [DataRow("https://www.buymeacoffee.com/YOUR_USERNAME")]
    [DataRow("https://buymeacoffee.com/YOUR_USERNAME")]
    public void TryValidateUrl_AcceptsAllowedHosts(string url)
    {
        var valid = ExternalLinkLauncher.TryValidateUrl(url, out var uri, out _);
        Assert.IsTrue(valid);
        Assert.AreEqual(Uri.UriSchemeHttps, uri.Scheme);
    }

    [TestMethod]
    public void TryLaunch_RejectsInvalidUrlWithoutThrowing()
    {
        var launcher = new ExternalLinkLauncher();
        var result = launcher.TryLaunch("http://github.com/mtalavi/Awayra");
        Assert.IsFalse(result.Success);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.IsNotNull(result.Url);
    }
}
