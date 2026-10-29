using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskbarGroups.Config;
using TaskbarGroups.Icons;
using TaskbarGroups.Switching;
using Windows.UI;

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
public sealed class AppTile(AppShortcut shortcut, ImageSource? icon, double iconSize) : INotifyPropertyChanged
{
    // Windows 11's taskbar: a short grey bar while running, a longer accent-coloured one for the app in front
    // (.active-dot and .active-dot.focus in design/taskbar_groups.html).

    private RunningState _state;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Who to look for among the open windows.</summary>
    public AppIdentity Identity { get; } = AppIdentity.Parse(shortcut.ResolvePath(), new Shell.ShortcutReader());

    public RunningState State
    {
        get => _state;
        set
        {
            if (_state == value)
                return;
            _state = value;
            foreach (var name in new[] { nameof(IndicatorVisibility), nameof(IndicatorWidth), nameof(IndicatorBrush) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public Visibility IndicatorVisibility => _state == RunningState.NotRunning ? Visibility.Collapsed : Visibility.Visible;
    public double IndicatorWidth => _state == RunningState.Focused ? 16 : 6;
    public Brush IndicatorBrush => new SolidColorBrush(TaskbarTheme.Color(_state == RunningState.Focused ? "TgIndicatorFocused" : "TgIndicatorRunning"));

    public AppShortcut Shortcut => shortcut;
    public string Name => shortcut.Name;
    public ImageSource? Icon => icon;

    /// <summary>In DIPs, chosen so the icon maps 1:1 onto screen pixels.</summary>
    public double IconSize => iconSize;
    public string Initial => string.IsNullOrEmpty(shortcut.Name) ? "?" : shortcut.Name[..1].ToUpperInvariant();
    public Visibility InitialVisibility => icon is null ? Visibility.Visible : Visibility.Collapsed;
}
