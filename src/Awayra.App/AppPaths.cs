namespace Awayra.App;

public static class AppPaths
{
    public static string DataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Awayra");

    public static string SettingsPath => Path.Combine(DataRoot, "settings.json");
    public static string StatePath => Path.Combine(DataRoot, "state.json");
    public static string StatisticsPath => Path.Combine(DataRoot, "stats.json");
    public static string LogsDirectory => Path.Combine(DataRoot, "Logs");
    public static string LogFilePath => Path.Combine(LogsDirectory, "awayra.log");

    public static void EnsureDataRoot()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LogsDirectory);
    }
}
