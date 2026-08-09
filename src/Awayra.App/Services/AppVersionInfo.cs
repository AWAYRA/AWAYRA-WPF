using System.Globalization;
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

            // Build is -1 only for a two-part version; Revision is never negative here, so the old
            // second branch could not be reached.
            var patch = version.Build < 0 ? 0 : version.Build;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Version {version.Major}.{version.Minor}.{patch}");
        }
        catch
        {
            return "Version unavailable";
        }
    }
}
