namespace Awayra.App;

public static class AppPaths
{
    private static string? _overrideDataRoot;

    public static string? OverrideDataRoot
    {
        get => _overrideDataRoot;
        set => _overrideDataRoot = value;
    }

    public static string DataRoot =>
        _overrideDataRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Awayra");

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
