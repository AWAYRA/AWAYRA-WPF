using System.Reflection;

namespace Awayra.App.Services;

public static class AppVersionInfo
{
    public static string GetDisplayVersion()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is null)
            {
                return "Version unavailable";
            }

            if (version.Revision >= 0)
            {
                return $"Version {version.Major}.{version.Minor}.{version.Build}";
            }

            return $"Version {version.Major}.{version.Minor}.0";
        }
        catch
        {
            return "Version unavailable";
        }
    }
}
