namespace TaskbarGroups.Switching;

/// <summary>What a .lnk file points at: its target program and the AppUserModelID it carries (Store app shortcuts).</summary>
public sealed record ShortcutInfo(string? Target, string? Aumid);

/// <summary>Reads .lnk files. Behind an interface so matching can be tested without the Shell.</summary>
public interface IShortcutReader
{
    ShortcutInfo? Read(string lnkPath);
}

/// <summary>
/// How to recognise a shortcut's running windows. Layers; a window matches if any layer matches, and a shortcut that
/// yields none of them matches nothing (a wrong match is worse than no match):
/// <list type="number">
/// <item>AppUserModelID (how the taskbar groups windows): from <c>shell:AppsFolder\&lt;AppID&gt;</c>, or a .lnk's own ID.</item>
/// <item>The program's .exe path: typed directly, or a .lnk's target. Store packages keep the version in the folder
/// name, so a path into <c>WindowsApps</c> is compared without it and survives updates.</item>
/// </list>
/// Never matched: a package family alone (one package can hold several apps), a bare file name, or a host process.
/// </summary>
public sealed record AppIdentity(string? Aumid, string? ExePath)
{
    private const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>Processes that host other apps' windows: sharing one says nothing about being the same app.</summary>
    private static readonly string[] HostExes = ["applicationframehost.exe"];

    /// <summary>Reads a path as typed into a shortcut (environment variables already expanded).</summary>
    public static AppIdentity Parse(string path, IShortcutReader? shortcuts = null)
    {
        if (path.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase))
            return new AppIdentity(path[AppsFolder.Length..], null);
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return new AppIdentity(null, path);
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && shortcuts?.Read(path) is { } info)
        {
            var target = info.Target is { } t && t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? Environment.ExpandEnvironmentVariables(t)
                : null;
            return new AppIdentity(string.IsNullOrWhiteSpace(info.Aumid) ? null : info.Aumid, target);
        }
        return new AppIdentity(null, null); // folders and URLs: no way to tell yet
    }

    /// <summary>The window's AppUserModelID (if it has one) or its process's image path says it's this app.</summary>
    public bool Matches(string? windowAumid, string? windowExePath) =>
        (Aumid is not null && string.Equals(Aumid, windowAumid, StringComparison.OrdinalIgnoreCase))
        || SameExe(ExePath, windowExePath);

    private static bool SameExe(string? wanted, string? actual)
    {
        if (wanted is null || actual is null || IsHost(wanted) || IsHost(actual))
            return false;
        if (string.Equals(wanted, actual, StringComparison.OrdinalIgnoreCase))
            return true;
        var key = PackageKey(wanted);
        return key is not null && string.Equals(key, PackageKey(actual), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHost(string exe) =>
        HostExes.Contains(Path.GetFileName(exe), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <c>...\WindowsApps\Name_1.2.3.0_x64__publisher\rest.exe</c> becomes <c>Name|x64|publisher|rest.exe</c>, dropping
    /// the version; null for any other path.
    /// </summary>
    internal static string? PackageKey(string path)
    {
        var parts = path.Split('\\', '/');
        var at = Array.FindIndex(parts, p => p.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase));
        if (at < 0 || at + 2 >= parts.Length)
            return null;
        // Name_version_architecture_resourceId_publisherId
        var fields = parts[at + 1].Split('_');
        if (fields.Length != 5)
            return null;
        return $"{fields[0]}|{fields[2]}|{fields[4]}|{string.Join('\\', parts[(at + 2)..])}";
    }

    /// <summary>Whether any of the open windows belong to this app, and whether one of them is in front.</summary>
    public RunningState StateIn(IReadOnlyList<WindowInfo> windows, nint foreground)
    {
        if (this is { Aumid: null, ExePath: null })
            return RunningState.NotRunning;
        var state = RunningState.NotRunning;
        foreach (var window in windows)
        {
            if (!Matches(window.Aumid, window.ExePath))
                continue;
            if (window.Handle == foreground)
                return RunningState.Focused;
            state = RunningState.Running;
        }
        return state;
    }
}

/// <summary>A window that has a taskbar button, with what's needed to tell which app it belongs to.</summary>
public readonly record struct WindowInfo(nint Handle, string? Aumid, string? ExePath);

public enum RunningState { NotRunning, Running, Focused }

public enum ClickKind { Launch, Activate, Minimize }

/// <summary>What clicking an app should do, given its windows and which window is in front.</summary>
public readonly record struct ClickAction(ClickKind Kind, nint Window = 0)
{
    /// <param name="windows">The app's windows, most recently used first.</param>
    /// <param name="foreground">The window the user is working in.</param>
    /// <param name="newWindow">Shift was held: always start another instance.</param>
    public static ClickAction Decide(IReadOnlyList<nint> windows, nint foreground, bool newWindow = false)
    {
        if (newWindow || windows.Count == 0)
            return new ClickAction(ClickKind.Launch);
        if (windows.Contains(foreground))
            return new ClickAction(ClickKind.Minimize, foreground);
        return new ClickAction(ClickKind.Activate, windows[0]);
    }
}
