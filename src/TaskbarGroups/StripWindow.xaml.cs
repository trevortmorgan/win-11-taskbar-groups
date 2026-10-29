using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TaskbarGroups.Config;
using TaskbarGroups.Icons;
using TaskbarGroups.Layout;
using TaskbarGroups.Shell;
using Windows.Foundation;

namespace TaskbarGroups;

/// <summary>The borderless, transparent strip that sits on the taskbar and shows the groups.</summary>
public sealed partial class StripWindow : Window
{
    /// <summary>App icon size, the same as Windows' own taskbar icons.</summary>
    private const double IconDips = 24;

    /// <summary>The list that opens from the "Groups" button. Created the first time it's needed.</summary>
    private GroupsFlyout? _flyout;

    public StripWindow()
    {
        InitializeComponent();

        SystemBackdrop = new TransparentBackdrop();
        var presenter = OverlappedPresenter.Create();
        AppWindow.SetPresenter(presenter);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        AppWindow.IsShownInSwitchers = false;

        Handle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        WindowChrome.MakeOverlay(Handle);

        // Apps starting and closing don't tell us, so look again every second (foreground changes refresh at once).
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => RefreshRunning();
        timer.Start();
    }

    public nint Handle { get; }

    public FrameworkElement RootElement => Root;

    /// <summary>
    /// Shows these groups, with each app's real icon. Relative paths to group icons are relative to
    /// <paramref name="configDirectory"/>. Call on the UI thread.
    /// </summary>
    public async Task SetGroupsAsync(IReadOnlyList<Group> groups, string configDirectory)
    {
        // Load icons at exactly the screen pixels they'll occupy, and display them at that size in DIPs, so there's
        // no further scaling (which is what makes icons look soft).
        var scale = WindowChrome.GetDpi(Handle) / 96.0;
        var sizePx = (int)Math.Round(IconDips * scale);
        var sizeDips = sizePx / scale;
        var icons = await IconLoader.LoadAsync(groups.SelectMany(g => g.Apps).Select(a => a.ResolvePath()), sizePx);

        Pills.ItemsSource = groups
            .Select(g => new GroupView(g, ResolveIcon(g, configDirectory), g.Apps
                .Select(a => new AppTile(a, icons.GetValueOrDefault(a.ResolvePath()) is { } icon ? IconLoader.ToBitmap(icon) : null, sizeDips))
                .ToList()))
            .ToList();
        _flyout?.SetGroups(GroupViews);
        RefreshRunning();
    }

    /// <summary>
    /// Updates the running / in-front bars under each app. Cheap enough to call often: one pass over the open
    /// windows however many apps there are. Call on the UI thread.
    /// </summary>
    public void RefreshRunning()
    {
        var tiles = GroupViews.SelectMany(g => g.Apps).ToList();
        if (tiles.Count == 0)
            return;
        try
        {
            var windows = WindowSwitcher.Snapshot();
            var foreground = WindowChrome.GetForeground();
            foreach (var tile in tiles)
                tile.State = tile.Identity.StateIn(windows, foreground);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't check which apps are running", ex);
        }
    }

    private IReadOnlyList<GroupView> GroupViews => Pills.ItemsSource as IReadOnlyList<GroupView> ?? [];

    private static GroupIcon? ResolveIcon(Group group, string configDirectory)
    {
        if (GroupIcon.TryResolve(group.Icon, configDirectory, out var icon, out var error))
            return icon;
        if (error is not null)
            Log.Info($"Group \"{group.Name}\": {error}; showing the dot instead");
        return null;
    }

    /// <summary>Wide pills, or the single "Groups" button.</summary>
    public bool IsCollapsed
    {
        get => GroupsButton.IsHitTestVisible;
        set
        {
            // Both layouts stay in the tree (just invisible) so either can be measured at any time.
            GroupsButton.Opacity = value ? 1 : 0;
            GroupsButton.IsHitTestVisible = value;
            Pills.Opacity = value ? 0 : 1;
            Pills.IsHitTestVisible = !value;
            if (!value)
                CloseFlyout();
        }
    }

    /// <summary>Width in DIPs the given layout needs, or null until the content has loaded.</summary>
    public double? MeasureWidth(bool collapsed)
    {
        FrameworkElement element = collapsed ? GroupsButton : Pills;
        if (!element.IsLoaded)
            return null;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width;
    }

    private void OnAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AppTile tile })
            AppLauncher.Open(tile.Shortcut);
    }

    /// <summary>The "Groups" button opens (or closes) the list of every group above it.</summary>
    private void OnGroupsClick(object sender, RoutedEventArgs e)
    {
        if (_flyout is { IsOpen: true })
        {
            _flyout.Dismiss();
            return;
        }

        // Pressing this button while the flyout is open takes the focus away from it, which closes it as the mouse
        // goes down, just before this click arrives. The click was meant to close it, so don't open it again.
        if (_flyout is not null && Environment.TickCount64 - _flyout.ClosedAt < 500)
            return;

        if (_flyout is null)
        {
            _flyout = new GroupsFlyout();
            _flyout.SetGroups(GroupViews);
            _flyout.Dismissed += (_, _) => Chevron.Rotation = 0;
        }

        // Centre it over the button, in screen pixels.
        var scale = Root.XamlRoot.RasterizationScale;
        var offset = GroupsButton.TransformToVisual(Root).TransformPoint(new Point(0, 0));
        var left = AppWindow.Position.X + (int)Math.Round(offset.X * scale);
        var anchor = IntRect.FromSize(left, AppWindow.Position.Y, (int)Math.Round(GroupsButton.ActualWidth * scale), AppWindow.Size.Height);

        Chevron.CenterPoint = new Vector3((float)Chevron.ActualWidth / 2, (float)Chevron.ActualHeight / 2, 0);
        Chevron.Rotation = 180;
        _flyout.Open(anchor, scale);
    }

    /// <summary>Closes the flyout, e.g. because the strip is being hidden or there's room for the pills again.</summary>
    public void CloseFlyout() => _flyout?.Dismiss();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => ((App)Application.Current).ShowSettings();

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
