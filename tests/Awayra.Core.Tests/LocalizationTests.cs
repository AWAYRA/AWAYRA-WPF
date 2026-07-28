using System.Xml.Linq;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Services;

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
    public void PersianResourceFile_IsNotPresent()
    {
        Assert.IsFalse(File.Exists(Path.Combine(ResourcesPath, "Strings.fa.resx")));
    }

    [TestMethod]
    public void ArabicResourceFile_IsNotPresent()
    {
        Assert.IsFalse(File.Exists(Path.Combine(ResourcesPath, "Strings.ar.resx")));
    }

    [TestMethod]
    public void EnglishOnly_NoRtlHelpersRemain()
    {
        var localizationSource = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Awayra.Core", "Localization", "Localization.cs")));

        Assert.IsFalse(localizationSource.Contains("CultureDirection", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("LanguageResolver", StringComparison.Ordinal));
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

[TestClass]
public sealed class OverlayGlassSettingsTests
{
    [DataTestMethod]
    [DataRow(0, 1.0, 30.0)]
    [DataRow(25, 0.75, 27.0)]
    [DataRow(50, 0.50, 24.0)]
    [DataRow(75, 0.25, 21.0)]
    [DataRow(100, 0.0, 18.0)]
    [DataRow(125, 0.0, 13.0)]
    [DataRow(150, 0.0, 8.0)]
    public void GlassClarity_MapsTintAndBlur(int clarity, double tint, double blur)
    {
        Assert.AreEqual(tint, OverlayGlassSettings.BackgroundTintOpacityFromClarity(clarity), 0.001);
        Assert.AreEqual(blur, OverlayGlassSettings.BlurRadiusFromClarity(clarity), 0.001);
    }

    [TestMethod]
    public void DefaultGlassClarity_IsOneHundred()
    {
        Assert.AreEqual(100, OverlayGlassSettings.DefaultGlassClarity);
        Assert.AreEqual(100, AppSettings.CreateDefault().GlassClarity);
    }

    [TestMethod]
    public void MinimumGlassClarity_IsZero()
    {
        Assert.AreEqual(0, OverlayGlassSettings.MinGlassClarity);
        Assert.AreEqual(0, OverlayGlassSettings.NormalizeGlassClarity(-5));
        Assert.AreEqual(1.0, OverlayGlassSettings.BackgroundTintOpacityFromClarity(0), 0.001);
    }

    [TestMethod]
    public void MaximumGlassClarity_IsOneHundredFifty()
    {
        Assert.AreEqual(150, OverlayGlassSettings.MaxGlassClarity);
        Assert.AreEqual(150, OverlayGlassSettings.NormalizeGlassClarity(200));
        Assert.AreEqual(0.0, OverlayGlassSettings.BackgroundTintOpacityFromClarity(150), 0.001);
    }

    [TestMethod]
    public void BackgroundVisibilityMigration_MapsLegacyRange()
    {
        Assert.AreEqual(0, OverlayGlassSettings.MigrateFromBackgroundVisibility(10));
        Assert.AreEqual(50, OverlayGlassSettings.MigrateFromBackgroundVisibility(20));
        Assert.AreEqual(100, OverlayGlassSettings.MigrateFromBackgroundVisibility(30));
        Assert.AreEqual(25, OverlayGlassSettings.MigrateFromBackgroundVisibility(15));
        Assert.AreEqual(75, OverlayGlassSettings.MigrateFromBackgroundVisibility(25));
    }

    [TestMethod]
    public void GlassTransparencyMigration_PreservesClosestValue()
    {
        Assert.AreEqual(75, OverlayGlassSettings.MigrateFromGlassTransparency(75));
    }

    [TestMethod]
    public void MalformedLegacyOpacity_FallsBackSafely()
    {
        Assert.AreEqual(100, OverlayGlassSettings.MigrateFromLegacyOpacity(double.NaN));
        Assert.AreEqual(100, OverlayGlassSettings.MigrateFromLegacyOpacity(double.PositiveInfinity));
    }

    [TestMethod]
    public void ContentOpacity_RemainsFullyOpaque()
    {
        Assert.AreEqual(1.0, OverlayGlassSettings.ContentOpacity, 0.001);
    }
}
