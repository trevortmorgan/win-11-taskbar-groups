using System.Diagnostics;
using System.IO;
using TaskbarGroups.Config;
using TaskbarGroups.Shell;
using TaskbarGroups.Switching;

namespace TaskbarGroups;

internal static class AppLauncher
{
    /// <summary>
    /// What a click on an app does, like a pinned taskbar button: switch to it if it's running (minimising it if it's
    /// already in front), otherwise launch it. Shift+click always opens a new window.
    /// <paramref name="front"/> is the window the user was working in, when that's no longer the foreground window.
    /// </summary>
    public static void Open(AppShortcut app, nint front = 0)
    {
        try
        {
            var windows = WindowSwitcher.FindWindows(AppIdentity.Parse(app.ResolvePath()));
            var foreground = front != 0 ? front : WindowChrome.GetForeground();
            var action = ClickAction.Decide(windows, foreground, WindowSwitcher.IsShiftDown());
            switch (action.Kind)
            {
                case ClickKind.Minimize:
                    WindowSwitcher.Minimize(action.Window);
                    Log.Info($"Minimised {app.Name}");
                    return;
                case ClickKind.Activate:
                    WindowSwitcher.Activate(action.Window);
                    Log.Info($"Switched to {app.Name}");
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't switch to {app.Name}; launching instead", ex);
        }
        Launch(app);
    }

    /// <summary>
    /// Opens the shortcut the way the Run box would, so .exe, .lnk, folders, URLs and
    /// shell:AppsFolder\&lt;AppID&gt; Store apps all work.
    /// </summary>
    public static void Launch(AppShortcut app)
    {
        var path = app.ResolvePath();
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Arguments = app.Arguments ?? "",
            // Programs expect to start in their own folder, not ours.
            WorkingDirectory = File.Exists(path) ? Path.GetDirectoryName(path) ?? "" : "",
        };

        try
        {
            Process.Start(start)?.Dispose();
            Log.Info($"Launched {app.Name} ({path})");
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't launch {app.Name} ({path})", ex);
        }
    }
}
