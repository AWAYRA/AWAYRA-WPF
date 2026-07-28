using Microsoft.Win32;
using Awayra.Core.Abstractions;

namespace Awayra.App.Services;

public sealed class RegistryAutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Awayra";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string;
    }

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        key.SetValue(ValueName, $"\"{executablePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key?.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, false);
        }
    }

    public void RepairIfStale(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        if (key?.GetValue(ValueName) is string existing)
        {
            var normalized = existing.Trim('"');
            if (!string.Equals(normalized, executablePath, StringComparison.OrdinalIgnoreCase))
            {
                Enable(executablePath);
            }
        }
    }
}
