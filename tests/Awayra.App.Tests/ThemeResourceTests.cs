using System.Windows;
using System.Xml.Linq;
using Awayra.App.Tests.Support;
using Awayra.Core.Coordination;

namespace Awayra.App.Tests;

[TestClass]
public sealed class ThemeResourceTests
{
    [TestMethod]
    public void RequiredBrushResources_ExistInThemeDictionary()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var merged = Application.Current.Resources.MergedDictionaries
                .First(d => d.Source?.LocalPath?.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true
                            || d.Source?.OriginalString?.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var key in ThemeResourceContract.RequiredBrushKeys)
            {
                Assert.IsTrue(merged.Contains(key), $"Missing brush resource: {key}");
                Assert.IsInstanceOfType(merged[key], typeof(System.Windows.Media.Brush), $"Resource {key} is not a brush.");
            }
        });
    }

    [TestMethod]
    public void RequiredStyleResources_ExistInThemeDictionary()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var merged = Application.Current.Resources.MergedDictionaries
                .First(d => d.Source?.LocalPath?.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true
                            || d.Source?.OriginalString?.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var key in ThemeResourceContract.RequiredStyleKeys)
            {
                Assert.IsTrue(merged.Contains(key), $"Missing style resource: {key}");
            }
        });
    }

    [TestMethod]
    public void DarkAndLightForegroundResources_Exist()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var merged = Application.Current.Resources.MergedDictionaries
                .First(d => d.Source?.LocalPath?.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true
                            || d.Source?.OriginalString?.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true);

            Assert.IsTrue(merged.Contains("PrimaryTextBrush"));
            Assert.IsTrue(merged.Contains("LightInputTextBrush"));
        });
    }

    [TestMethod]
    public void GlobalWindowStyle_DoesNotHideWindows()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var merged = Application.Current.Resources.MergedDictionaries
                .First(d => d.Source?.LocalPath?.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true
                            || d.Source?.OriginalString?.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true);

            var windowStyle = merged.Values.OfType<Style>().FirstOrDefault(s => s.TargetType == typeof(Window));
            Assert.IsNotNull(windowStyle);

            Assert.IsFalse(HasSetter(windowStyle, "Opacity", 0d));
            Assert.IsFalse(HasSetter(windowStyle, "Visibility", Visibility.Collapsed));
            Assert.IsFalse(HasSetter(windowStyle, "Visibility", Visibility.Hidden));
        });
    }

    private static bool HasSetter(Style style, string propertyName, object expected)
    {
        foreach (var setter in style.Setters.OfType<Setter>())
        {
            if (setter.Property?.Name == propertyName &&
                (setter.Value?.Equals(expected) == true || setter.Value?.ToString() == expected.ToString()))
            {
                return true;
            }
        }

        return false;
    }
}

[TestClass]
public sealed class ThemeXamlGuardTests
{
    private static readonly string ThemePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Awayra.App", "Resources", "Theme.xaml"));

    [TestMethod]
    public void ThemeXaml_DoesNotUseForbiddenStyleSetters()
    {
        Assert.IsTrue(File.Exists(ThemePath), $"Theme file not found: {ThemePath}");
        var xaml = XDocument.Load(ThemePath);
        var forbidden = ThemeResourceContract.ForbiddenStyleSetterProperties.ToHashSet(StringComparer.Ordinal);

        var offenders = xaml.Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .Select(e => e.Attribute("Property")?.Value)
            .Where(p => p is not null && forbidden.Contains(p))
            .ToArray();

        Assert.AreEqual(0, offenders.Length, $"Forbidden style setters found: {string.Join(", ", offenders)}");
    }
}
