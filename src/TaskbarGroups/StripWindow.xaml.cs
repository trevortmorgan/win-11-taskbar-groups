using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TaskbarGroups.Config;
using TaskbarGroups.Shell;
using Windows.Foundation;

namespace TaskbarGroups;

/// <summary>The borderless, transparent strip that sits on the taskbar and shows the groups.</summary>
public sealed partial class StripWindow : Window
{
    /// <summary>App icon size, the same as Windows' own taskbar icons.</summary>
    private const double IconDips = 24;

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
    }

    public nint Handle { get; }

    public FrameworkElement RootElement => Root;

    /// <summary>Shows these groups, with each app's real icon. Call on the UI thread.</summary>
    public async Task SetGroupsAsync(IReadOnlyList<Group> groups)
    {
        // Load icons at exactly the screen pixels they'll occupy, and display them at that size in DIPs, so there's
        // no further scaling (which is what makes icons look soft).
        var scale = WindowChrome.GetDpi(Handle) / 96.0;
        var sizePx = (int)Math.Round(IconDips * scale);
        var sizeDips = sizePx / scale;
        var icons = await IconLoader.LoadAsync(groups.SelectMany(g => g.Apps).Select(a => a.ResolvePath()), sizePx);

        Pills.ItemsSource = groups
            .Select(g => new GroupView(g, g.Apps
                .Select(a => new AppTile(a, icons.GetValueOrDefault(a.ResolvePath()) is { } icon ? IconLoader.ToBitmap(icon) : null, sizeDips))
                .ToList()))
            .ToList();
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
            AppLauncher.Launch(tile.Shortcut);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
