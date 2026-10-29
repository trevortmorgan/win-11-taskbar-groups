namespace TaskbarGroups.Windows;

/// <summary>
/// How to recognise a shortcut's running windows: by AppUserModelID (how the taskbar groups windows) for Store apps
/// written as <c>shell:AppsFolder\&lt;AppID&gt;</c>, or by the program's .exe path otherwise.
/// </summary>
public sealed record AppIdentity(string? Aumid, string? ExePath)
{
    private const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>Reads a path as typed into a shortcut (environment variables already expanded).</summary>
    public static AppIdentity Parse(string path)
    {
        if (path.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase))
            return new AppIdentity(path[AppsFolder.Length..], null);
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return new AppIdentity(null, path);
        return new AppIdentity(null, null); // folders, URLs, .lnk: no way to tell yet
    }

    /// <summary>The window's AppUserModelID (if it has one) or its process's image path says it's this app.</summary>
    public bool Matches(string? windowAumid, string? windowExePath) =>
        (Aumid is not null && string.Equals(Aumid, windowAumid, StringComparison.OrdinalIgnoreCase))
        || (ExePath is not null && string.Equals(ExePath, windowExePath, StringComparison.OrdinalIgnoreCase));
}

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
