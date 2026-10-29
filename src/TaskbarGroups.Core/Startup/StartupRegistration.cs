using Microsoft.Win32;

namespace TaskbarGroups.Startup;

/// <summary>
/// Starts the app when the user signs in, using a value under HKCU\...\Run (per user, no admin rights). The state is
/// always read back from the registry, so changes made elsewhere (e.g. in Task Manager's Startup tab) are respected.
/// </summary>
public sealed class StartupRegistration(string command, string valueName = "TaskbarGroups", string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    /// <summary>Registration for the running exe.</summary>
    public static StartupRegistration ForCurrentProcess() =>
        new(Quote(Environment.ProcessPath ?? throw new InvalidOperationException("Unknown process path.")));

    /// <summary>True when our value is present and points at this exe (a stale path from a moved copy counts as off).</summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string current
                && string.Equals(current, command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        if (enabled)
            key.SetValue(valueName, command, RegistryValueKind.String);
        else
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static string Quote(string path) => $"\"{path}\"";
}
