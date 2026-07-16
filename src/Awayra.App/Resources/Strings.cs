using System.Globalization;
using System.Resources;

namespace Awayra.App.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager = new("Awayra.App.Resources.Strings", typeof(Strings).Assembly);

    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;

    public static string Get(string name) => ResourceManager.GetString(name, Culture) ?? name;
}
