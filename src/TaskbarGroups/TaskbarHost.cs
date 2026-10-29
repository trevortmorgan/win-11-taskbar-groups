using Microsoft.UI.Dispatching;
using TaskbarGroups.Layout;
using TaskbarGroups.Shell;

namespace TaskbarGroups;

/// <summary>
/// Keeps one strip window in the free space on one taskbar (the primary, or a secondary one on another monitor):
/// re-reads that taskbar's layout every second, collapses or
/// hides the strip when space runs out, and steps back in front of the taskbar whenever the taskbar is clicked.
/// </summary>
internal sealed class TaskbarHost : IDisposable
{
    /// <summary>Clear space kept between us and any Windows control, in DIPs.</summary>
    private const double MarginDips = 8;

    private readonly StripWindow _window;
    private readonly nint? _taskbar; // null = the primary taskbar, whichever window Explorer currently has for it
    private readonly string _label;
    private readonly DispatcherQueueTimer _timer;
    private readonly ForegroundWatcher _foreground = new();
    private TaskbarProbe? _probe;
    private IntRect? _placedAt;
    private bool _updating;
    private string? _lastHideReason;
    private bool _userHidden;
    private bool _disposed;

    /// <param name="taskbar">The taskbar window to sit on, or null for the primary taskbar.</param>
    /// <param name="label">Names this taskbar in the log.</param>
    public TaskbarHost(StripWindow window, nint? taskbar, string label)
    {
        _window = window;
        _taskbar = taskbar;
        _label = label;
        _window.RootElement.Loaded += (_, _) => _ = UpdateAsync();
        _foreground.ForegroundChanged += OnForegroundChanged;

        // Polling is simple and also covers Explorer restarts, DPI/resolution changes and auto-hide.
        _timer = window.DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => _ = UpdateAsync();
        _timer.Start();
        _ = UpdateAsync();
    }

    /// <summary>True while the user has hidden the strip from the tray menu. Not remembered between launches.</summary>
    public bool UserHidden
    {
        get => _userHidden;
        set
        {
            if (_userHidden == value)
                return;
            _userHidden = value;
            _ = UpdateAsync();
        }
    }

    private void OnForegroundChanged(nint hwnd)
    {
        if (_placedAt is not null)
            WindowChrome.BringToTop(_window.Handle);
        _window.RefreshRunning();
        _ = UpdateAsync(); // e.g. a fullscreen game just came to the front
    }

    private async Task UpdateAsync()
    {
        if (_updating)
            return;
        _updating = true;
        try
        {
            // UI Automation must not run on the UI thread.
            var snapshot = await Task.Run(() => (_probe ??= new TaskbarProbe()).Read(_taskbar));
            if (!_disposed)
                Apply(snapshot);
        }
        catch (Exception ex)
        {
            // Typically Explorer restarting. Start afresh on the next tick.
            Log.Error($"Taskbar update failed ({_label})", ex);
            _probe = null;
            if (!_disposed)
                Hide("error");
        }
        finally
        {
            _updating = false;
        }
    }

    private void Apply(TaskbarSnapshot? taskbar)
    {
        var hideReason = taskbar switch
        {
            _ when _userHidden => "hidden from the tray menu",
            null => "no taskbar",
            { IsOnScreen: false } => "taskbar auto-hidden",
            { IsHorizontal: false } => "taskbar not horizontal",
            _ when FullscreenPolicy.IsFullscreenOn(taskbar.Monitor, WindowChrome.GetForegroundWindowInfo(), WindowChrome.WindowsReportsFullscreenApp()) => "fullscreen app on this monitor",
            _ => null,
        };
        if (hideReason is not null || taskbar is null)
        {
            Hide(hideReason ?? "no taskbar");
            return;
        }

        _window.SetDpi(taskbar.Dpi);
        var scale = taskbar.Dpi / 96.0;
        var gaps = TaskbarGapFinder.FindGaps(taskbar.Bounds, taskbar.Controls, (int)Math.Round(MarginDips * scale));
        var fullWidth = _window.MeasureWidth(collapsed: false);
        var compactWidth = _window.MeasureWidth(collapsed: true);

        if (fullWidth is null || compactWidth is null)
        {
            // Content hasn't loaded yet (it only loads once shown). Show it in the gap; Loaded triggers a proper pass.
            if (TaskbarGapFinder.PickGap(gaps, taskbar.AppButtonsRight, minWidth: 1) is { } anyGap)
                Show(anyGap);
            return;
        }

        if (fullWidth.Value <= 0)
        {
            Hide("no groups to show"); // still loading, or the config has none
            return;
        }

        var full = (int)Math.Ceiling(fullWidth.Value * scale);
        var compact = (int)Math.Ceiling(compactWidth.Value * scale);
        if (TaskbarGapFinder.PickGap(gaps, taskbar.AppButtonsRight, compact) is not { } gap)
        {
            Hide("no room, even for the Groups button");
            return;
        }

        _window.IsCollapsed = gap.Width < full;
        Show(IntRect.FromSize(gap.Left, taskbar.Bounds.Top, _window.IsCollapsed ? compact : full, taskbar.Bounds.Height));
    }

    private void Show(IntRect rect)
    {
        if (_placedAt == rect)
            return;
        _lastHideReason = null;
        if (_placedAt is null)
        {
            _window.AppWindow.Show(activateWindow: false);
            WindowChrome.MakeOverlay(_window.Handle); // WinUI restores its frame styles on show
        }
        WindowChrome.PlaceOnTop(_window.Handle, rect);
        _placedAt = rect;
        Log.Info($"Shown ({_label}) at {rect} ({(_window.IsCollapsed ? "collapsed" : "wide")})");
    }

    private void Hide(string reason)
    {
        if (_lastHideReason != reason)
            Log.Info($"Hidden ({_label}): {reason}");
        _lastHideReason = reason;
        if (_placedAt is null)
            return;
        _window.CloseFlyout();
        _window.AppWindow.Hide();
        _placedAt = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _foreground.Dispose();
    }
}
