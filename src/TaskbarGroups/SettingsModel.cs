using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using TaskbarGroups.Config;
using TaskbarGroups.Icons;

namespace TaskbarGroups;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>An app in a group while it's being edited. Shown with its real icon once that has loaded.</summary>
public sealed class EditableApp(AppShortcut shortcut) : Observable
{
    private string _name = shortcut.Name;
    private ImageSource? _icon;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string Path => shortcut.Path;

    public ImageSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    public AppShortcut ToShortcut() => shortcut with { Name = Name };
}

/// <summary>A group while it's being edited. <see cref="Changed"/> fires for any edit, including to its apps.</summary>
public sealed class EditableGroup : Observable
{
    private readonly string _configDirectory;
    private string _name;
    private string _color;
    private string? _icon;

    public EditableGroup(Group group, string configDirectory)
    {
        _configDirectory = configDirectory;
        _name = group.Name;
        _color = group.Color;
        _icon = group.Icon;
        Apps = [.. group.Apps.Select(a => new EditableApp(a))];
        foreach (var app in Apps)
            app.PropertyChanged += OnAppChanged;
        Apps.CollectionChanged += OnAppsChanged;
    }

    public event EventHandler? Changed;

    public ObservableCollection<EditableApp> Apps { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>"#RRGGBB".</summary>
    public string Color
    {
        get => _color;
        set
        {
            if (Set(ref _color, value))
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A bundled icon name or the path to an .svg, or null for the dot.</summary>
    public string? Icon
    {
        get => _icon;
        set
        {
            if (Set(ref _icon, value))
            {
                Raise(nameof(ResolvedIcon));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>For <see cref="GroupMark"/>; null when there's no icon or it can't be loaded.</summary>
    public GroupIcon? ResolvedIcon => GroupIcon.TryResolve(Icon, _configDirectory, out var icon, out _) ? icon : null;

    public Group ToGroup() =>
        new(string.IsNullOrWhiteSpace(Name) ? "Untitled" : Name.Trim(), Color, [.. Apps.Select(a => a.ToShortcut())], Icon);

    private void OnAppChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditableApp.Name))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAppsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (EditableApp app in e.NewItems ?? Array.Empty<EditableApp>())
            app.PropertyChanged += OnAppChanged;
        foreach (EditableApp app in e.OldItems ?? Array.Empty<EditableApp>())
            app.PropertyChanged -= OnAppChanged;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>One choice in the icon picker.</summary>
public sealed class IconChoice(string name, GroupIcon icon, string color) : Observable
{
    private string _color = color;

    public string Name => name;
    public GroupIcon Icon => icon;

    public string Color
    {
        get => _color;
        set => Set(ref _color, value);
    }
}

/// <summary>An installed app in the "choose from installed apps" list.</summary>
public sealed class InstalledAppChoice(StartApp app)
{
    public StartApp App => app;
    public string Name => app.Name;
}
