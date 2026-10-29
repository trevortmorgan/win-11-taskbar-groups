using Microsoft.UI.Dispatching;
using TaskbarGroups.Layout;
using TaskbarGroups.Shell;

namespace TaskbarGroups;

/// <summary>
/// Keeps the strip window in the free space on the taskbar: re-reads the taskbar layout every second, collapses or
/// hides the strip when space runs out, and steps back in front of the taskbar whenever the taskbar is clicked.
/// </summary>
internal sealed class TaskbarHost : IDisposable
{
    /// <summary>Clear space kept between us and any Windows control, in DIPs.</summary>
    private const double MarginDips = 8;

    private readonly StripWindow _window;
    private readonly DispatcherQueueTimer _timer;
    private readonly ForegroundWatcher _foreground = new();
    private TaskbarProbe? _probe;
    private IntRect? _placedAt;
    private bool _updating;
    private string? _lastHideReason;

    public TaskbarHost(StripWindow window)
    {
        _window = window;
        _window.RootElement.Loaded += (_, _) => _ = UpdateAsync();
        _foreground.ForegroundChanged += OnForegroundChanged;

        // Polling is simple and also covers Explorer restarts, DPI/resolution changes and auto-hide.
        _timer = window.DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => _ = UpdateAsync();
        _timer.Start();
        _ = UpdateAsync();
    }

    private void OnForegroundChanged(nint hwnd)
    {
        if (_placedAt is not null)
            WindowChrome.BringToTop(_window.Handle);
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
            var snapshot = await Task.Run(() => (_probe ??= new TaskbarProbe()).Read());
            Apply(snapshot);
        }
        catch (Exception ex)
        {
            // Typically Explorer restarting. Start afresh on the next tick.
            Log.Error("Taskbar update failed", ex);
            _probe = null;
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
            null => "no taskbar",
            { IsOnScreen: false } => "taskbar auto-hidden",
            { IsHorizontal: false } => "taskbar not horizontal",
            _ when WindowChrome.IsFullscreenAppRunning() => "fullscreen app running",
            _ => null,
        };
        if (hideReason is not null || taskbar is null)
        {
            Hide(hideReason ?? "no taskbar");
            return;
        }

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
        Log.Info($"Shown at {rect} ({(_window.IsCollapsed ? "collapsed" : "wide")})");
    }

    private void Hide(string reason)
    {
        if (_lastHideReason != reason)
            Log.Info($"Hidden: {reason}");
        _lastHideReason = reason;
        if (_placedAt is null)
            return;
        _window.AppWindow.Hide();
        _placedAt = null;
    }

    public void Dispose()
    {
        _timer.Stop();
        _foreground.Dispose();
    }
}
