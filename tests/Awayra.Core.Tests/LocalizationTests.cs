using System.Xml.Linq;
using Awayra.Core.Localization;
using Awayra.Core.Models;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class LocalizationTests
{
    private static readonly string ResourcesPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Awayra.App", "Resources"));

    [TestMethod]
    public void AllKeys_ExistInEnglishResources()
    {
        AssertAllKeysPresent("Strings.resx");
    }

    [TestMethod]
    public void AllKeys_ExistInPersianResources()
    {
        AssertAllKeysPresent("Strings.fa.resx");
    }

    [TestMethod]
    public void AllKeys_ExistInArabicResources()
    {
        AssertAllKeysPresent("Strings.ar.resx");
    }

    [TestMethod]
    public void RtlSelection_PersianAndArabic()
    {
        Assert.IsTrue(CultureDirection.IsRightToLeft("fa-IR"));
        Assert.IsTrue(CultureDirection.IsRightToLeft("ar-SA"));
        Assert.IsFalse(CultureDirection.IsRightToLeft("en-US"));
    }

    [TestMethod]
    public void LanguageResolver_ReturnsExpectedCultures()
    {
        Assert.AreEqual("en", LanguageResolver.ResolveCulture(AppLanguage.English));
        Assert.AreEqual("fa", LanguageResolver.ResolveCulture(AppLanguage.Persian));
        Assert.AreEqual("ar", LanguageResolver.ResolveCulture(AppLanguage.Arabic));
    }

    private static void AssertAllKeysPresent(string fileName)
    {
        var path = Path.Combine(ResourcesPath, fileName);
        Assert.IsTrue(File.Exists(path), $"Resource file not found: {path}");

        var doc = XDocument.Load(path);
        var values = doc.Descendants("data")
            .Where(e => e.Attribute("name") is not null && e.Element("value") is not null)
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")!.Value,
                StringComparer.Ordinal);

        foreach (var key in StringKeys.All)
        {
            Assert.IsTrue(values.TryGetValue(key, out var value), $"Missing key {key} in {fileName}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Empty value for {key} in {fileName}");
        }
    }
}
