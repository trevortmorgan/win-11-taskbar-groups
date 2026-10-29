using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskbarGroups.Theming;
using Windows.UI;

namespace TaskbarGroups;

/// <summary>
/// The current taskbar theme, and the lookup into the palette. The colours themselves live in the Light and Dark
/// theme dictionaries in App.xaml, so a theme is a palette swap and nothing else hard-codes a colour for it.
/// </summary>
internal static class TaskbarTheme
{
    public static bool IsLight { get; private set; } = WindowsTheme.ReadTaskbarIsLight();

    public static ElementTheme Element => IsLight ? ElementTheme.Light : ElementTheme.Dark;

    /// <summary>Raised on the UI thread after the Windows theme changed.</summary>
    public static event Action? Changed;

    /// <summary>Re-reads the setting and raises <see cref="Changed"/> if it differs. Cheap; call from the UI thread.</summary>
    public static void Refresh()
    {
        var light = WindowsTheme.ReadTaskbarIsLight();
        if (light == IsLight)
            return;
        IsLight = light;
        Changed?.Invoke();
    }

    /// <summary>A colour from the current theme's palette (App.xaml).</summary>
    public static Color Color(string key)
    {
        var themes = Application.Current.Resources.ThemeDictionaries;
        var palette = (ResourceDictionary)themes[IsLight ? "Light" : "Dark"];
        return ((SolidColorBrush)palette[key]).Color;
    }
}
