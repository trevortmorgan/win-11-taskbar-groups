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
    public StripWindow(IReadOnlyList<Group> groups)
    {
        InitializeComponent();
        Pills.ItemsSource = groups;

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

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
