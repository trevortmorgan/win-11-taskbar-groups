using Microsoft.Win32;

namespace TaskbarGroups.Theming;

/// <summary>
/// Whether the taskbar is light or dark. That's Windows mode (<c>SystemUsesLightTheme</c>), not app mode
/// (<c>AppsUseLightTheme</c>): the taskbar and its flyouts follow the former, ordinary app windows the latter.
/// </summary>
public static class WindowsTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Light only when the value is the DWORD 1. Missing or anything else is dark, which is what the strip used to be.</summary>
    public static bool IsLight(object? registryValue) => registryValue is int and 1;

    /// <summary>True when the taskbar is currently light. Read fresh each time.</summary>
    public static bool ReadTaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return IsLight(key?.GetValue("SystemUsesLightTheme"));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
