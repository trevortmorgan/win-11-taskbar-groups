using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskbarGroups.Config;
using TaskbarGroups.Icons;

namespace TaskbarGroups;

/// <summary>A group as shown on the strip.</summary>
public sealed class GroupView(Group group, GroupIcon? icon, IReadOnlyList<AppTile> apps)
{
    public string Name => group.Name;
    public string Color => group.Color;

    /// <summary>Null for the plain dot. Show it with <see cref="GroupMark"/>.</summary>
    public GroupIcon? Icon => icon;
    public IReadOnlyList<AppTile> Apps => apps;
}

/// <summary>One clickable app in a group: its real icon, or its initial if Windows has no icon for it.</summary>
public sealed class AppTile(AppShortcut shortcut, ImageSource? icon, double iconSize)
{
    public AppShortcut Shortcut => shortcut;
    public string Name => shortcut.Name;
    public ImageSource? Icon => icon;

    /// <summary>In DIPs, chosen so the icon maps 1:1 onto screen pixels.</summary>
    public double IconSize => iconSize;
    public string Initial => string.IsNullOrEmpty(shortcut.Name) ? "?" : shortcut.Name[..1].ToUpperInvariant();
    public Visibility InitialVisibility => icon is null ? Visibility.Visible : Visibility.Collapsed;
}
