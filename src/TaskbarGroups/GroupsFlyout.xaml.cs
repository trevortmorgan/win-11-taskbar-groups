using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TaskbarGroups.Layout;
using TaskbarGroups.Shell;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;

namespace TaskbarGroups;

/// <summary>
/// The list of every group that opens above the "Groups" button when the strip is collapsed. Unlike the strip it's
/// an ordinary activatable window, so it gets the keyboard (Esc, arrows, Tab, Enter) and knows when you click away.
/// </summary>
public sealed partial class GroupsFlyout : Window
{
    /// <summary>Space between the flyout and the top of the taskbar, in DIPs.</summary>
    private const double GapDips = 8;

    private IntRect _anchor;
    private double _scale = 1;

    /// <summary>The window that had the keyboard before the flyout opened.</summary>
    private nint _returnFocusTo;

    public GroupsFlyout()
    {
        InitializeComponent();

        SystemBackdrop = new TransparentBackdrop();
        var presenter = OverlappedPresenter.Create();
        AppWindow.SetPresenter(presenter);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        AppWindow.IsShownInSwitchers = false;

        Handle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        WindowChrome.MakePopup(Handle);

        Activated += OnActivated;
        AppWindow.Closing += (_, e) =>
        {
            // Alt+F4 would destroy the window for good; just hide it instead.
            e.Cancel = true;
            DismissToPrevious();
        };
        // The first time it opens, its content only loads once shown, so it can only be sized properly then.
        Root.Loaded += (_, _) =>
        {
            Place();
            FocusFirstApp();
        };
    }

    public nint Handle { get; }

    public bool IsOpen { get; private set; }

    /// <summary>When it last closed (<see cref="Environment.TickCount64"/>).</summary>
    public long ClosedAt { get; private set; }

    /// <summary>Raised whenever the flyout closes, however that happened.</summary>
    public event EventHandler? Dismissed;

    public void SetGroups(IReadOnlyList<GroupView> groups)
    {
        GroupList.ItemsSource = groups;
        if (IsOpen)
            Place();
    }

    /// <summary>Opens centred above <paramref name="anchor"/> (screen pixels; <paramref name="scale"/> is its DPI / 96).</summary>
    public void Open(IntRect anchor, double scale)
    {
        _anchor = anchor;
        _scale = scale;
        _returnFocusTo = WindowChrome.GetForeground();
        Place();
        AppWindow.Show(activateWindow: true);
        WindowChrome.MakePopup(Handle); // WinUI restores its frame styles on show
        WindowChrome.Activate(Handle);
        IsOpen = true;
        Log.Info("Flyout opened");
        Scroller.ChangeView(null, 0, null, disableAnimation: true);
        FocusFirstApp();
    }

    /// <summary>So the arrow keys, Tab and Enter work straight away.</summary>
    private void FocusFirstApp()
    {
        if (FocusManager.FindFirstFocusableElement(GroupList) is Control first)
            first.Focus(FocusState.Programmatic);
    }

    public void Dismiss()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        ClosedAt = Environment.TickCount64;
        Log.Info("Flyout closed");
        AppWindow.Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Closes and gives the keyboard back to the window that had it before we opened (for Esc and Alt+F4).
    /// Otherwise Windows hands it to the next window down, which is the strip.
    /// </summary>
    private void DismissToPrevious()
    {
        if (IsOpen && _returnFocusTo != 0)
            WindowChrome.SetForeground(_returnFocusTo);
        Dismiss();
    }

    /// <summary>Sizes the window to fit the list (scrolling if the screen is too short) and puts it above the anchor.</summary>
    private void Place()
    {
        var work = DisplayArea.GetFromPoint(new PointInt32(_anchor.Left, _anchor.Top), DisplayAreaFallback.Nearest).WorkArea;
        var gap = (int)Math.Round(GapDips * _scale);
        var bottom = Math.Min(_anchor.Top, work.Y + work.Height) - gap;
        var maxHeight = Math.Max(0, bottom - work.Y - gap) / _scale;

        Panel.MaxHeight = maxHeight;
        Panel.Measure(new Size(Panel.Width, maxHeight));
        // Until the content has loaded there's nothing to measure, so take the full height; Loaded corrects it.
        var heightDips = Root.IsLoaded ? Panel.DesiredSize.Height : maxHeight;

        var width = (int)Math.Ceiling(Panel.Width * _scale);
        var height = (int)Math.Ceiling(heightDips * _scale);
        var left = _anchor.Left + (_anchor.Width - width) / 2;
        left = Math.Clamp(left, work.X, Math.Max(work.X, work.X + work.Width - width));
        AppWindow.MoveAndResize(new RectInt32(left, bottom - height, width, height));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Clicking anywhere else (including the taskbar) takes the focus away.
        if (args.WindowActivationState == WindowActivationState.Deactivated)
            Dismiss();
    }

    /// <summary>
    /// The arrow keys move between apps. Handled before the ScrollViewer sees them, otherwise it uses them to scroll
    /// and the focus only moves once the focused app has scrolled out of sight.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        FocusNavigationDirection? direction = e.Key switch
        {
            VirtualKey.Up => FocusNavigationDirection.Up,
            VirtualKey.Down => FocusNavigationDirection.Down,
            VirtualKey.Left => FocusNavigationDirection.Left,
            VirtualKey.Right => FocusNavigationDirection.Right,
            _ => null,
        };
        if (direction is null)
            return;
        FocusManager.TryMoveFocus(direction.Value, new FindNextElementOptions
        {
            SearchRoot = GroupList.XamlRoot.Content,
            // Up/Down go to the nearest app in the next group, even if that group's row is shorter.
            XYFocusNavigationStrategyOverride = e.Key is VirtualKey.Up or VirtualKey.Down
                ? XYFocusNavigationStrategyOverride.NavigationDirectionDistance
                : XYFocusNavigationStrategyOverride.None,
        });
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            DismissToPrevious();
            e.Handled = true;
        }
    }

    private void OnAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AppTile tile })
        {
            // Act while we still have the focus, so Windows lets the app come to the front.
            AppLauncher.Open(tile.Shortcut, _returnFocusTo);
            Dismiss();
        }
    }
}
