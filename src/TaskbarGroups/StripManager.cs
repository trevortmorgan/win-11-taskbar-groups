using Microsoft.UI.Dispatching;
using TaskbarGroups.Config;
using TaskbarGroups.Layout;
using TaskbarGroups.Shell;

namespace TaskbarGroups;

/// <summary>
/// Puts a strip on every taskbar: one on the primary taskbar, and one on each secondary taskbar (the other monitors,
/// when *Show my taskbar on all displays* is on). Every strip shows the same groups and finds its own free space,
/// collapses or hides independently. Strips are added and removed as monitors come and go.
/// </summary>
internal sealed class StripManager : IDisposable
{
    private sealed record Entry(StripWindow Window, TaskbarHost Host);

    private readonly Entry _primary;
    private readonly Dictionary<nint, Entry> _secondary = [];
    private readonly DispatcherQueueTimer _timer;
    private IReadOnlyList<Group>? _groups;
    private string _configDirectory = "";
    private bool _userHidden;
    private bool _disposed;

    public StripManager()
    {
        var window = new StripWindow();
        _primary = new Entry(window, new TaskbarHost(window, taskbar: null, "primary"));

        // Monitors can be plugged in at any time (and Explorer recreates the taskbars when it restarts), and there's
        // no cheap notification for the taskbar windows themselves, so look again every second.
        _timer = window.DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => SyncSecondaryStrips();
        _timer.Start();
        SyncSecondaryStrips();
    }

    /// <summary>The strip on the primary taskbar. It lives as long as the app does.</summary>
    public StripWindow Primary => _primary.Window;

    /// <summary>True while the user has hidden the strips from the tray menu. Not remembered between launches.</summary>
    public bool UserHidden
    {
        get => _userHidden;
        set
        {
            _userHidden = value;
            _primary.Host.UserHidden = value;
            foreach (var entry in _secondary.Values)
                entry.Host.UserHidden = value;
        }
    }

    /// <summary>Shows these groups on every strip, now and on any strip added later. Call on the UI thread.</summary>
    public async Task SetGroupsAsync(IReadOnlyList<Group> groups, string configDirectory)
    {
        _groups = groups;
        _configDirectory = configDirectory;
        await Task.WhenAll(AllWindows().Select(w => w.SetGroupsAsync(groups, configDirectory)));
    }

    private IEnumerable<StripWindow> AllWindows() => _secondary.Values.Select(e => e.Window).Prepend(_primary.Window).ToList();

    private void SyncSecondaryStrips()
    {
        if (_disposed)
            return;
        try
        {
            var (added, removed) = TaskbarSync.Diff(TaskbarProbe.FindSecondaryTaskbars(), _secondary.Keys);
            foreach (var taskbar in removed)
            {
                Log.Info($"Taskbar {taskbar:X} went away; removing its strip");
                var entry = _secondary[taskbar];
                _secondary.Remove(taskbar);
                entry.Host.Dispose();
                entry.Window.Close();
            }
            foreach (var taskbar in added)
                AddSecondary(taskbar);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't update the strips for the other monitors", ex);
        }
    }

    private void AddSecondary(nint taskbar)
    {
        Log.Info($"Found another taskbar ({taskbar:X}); adding a strip");
        var window = new StripWindow();
        var host = new TaskbarHost(window, taskbar, $"monitor {taskbar:X}");
        host.UserHidden = _userHidden;
        _secondary[taskbar] = new Entry(window, host);
        if (_groups is not null)
            _ = window.SetGroupsAsync(_groups, _configDirectory);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _primary.Host.Dispose();
        foreach (var entry in _secondary.Values)
        {
            entry.Host.Dispose();
            entry.Window.Close();
        }
        _secondary.Clear();
    }
}
