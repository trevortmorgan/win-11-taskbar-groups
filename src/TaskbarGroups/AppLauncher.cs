using System.Diagnostics;
using System.IO;
using TaskbarGroups.Config;

namespace TaskbarGroups;

internal static class AppLauncher
{
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
