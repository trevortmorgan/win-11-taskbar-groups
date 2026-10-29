using System.Text.Json.Serialization;

namespace TaskbarGroups.Config;

/// <summary>
/// One app shortcut inside a group. <see cref="Path"/> is anything you could type into the Run box:
/// an .exe, a .lnk shortcut, a folder, a URL, or a Store app as <c>shell:AppsFolder\&lt;AppID&gt;</c>
/// (see <c>Get-StartApps</c> in PowerShell for app IDs). Environment variables such as %WINDIR% are expanded.
/// </summary>
public sealed record AppShortcut(string Name, string Path, string? Arguments = null)
{
    public string ResolvePath() => Environment.ExpandEnvironmentVariables(Path);
}

/// <summary>
/// A named, coloured set of app shortcuts shown as one pill on the taskbar. <see cref="Icon"/> is optional: a bundled
/// icon name or a path to an .svg file (see <see cref="Icons.GroupIcon"/>). Without one, the pill shows a dot.
/// </summary>
public sealed record Group(
    string Name,
    string Color,
    [property: JsonPropertyOrder(1)] IReadOnlyList<AppShortcut> Apps, // Written after "icon", so the short fields come first.
    string? Icon = null);

public sealed record GroupsConfig(IReadOnlyList<Group> Groups)
{
    private const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>Written to disk on first run as a starting point. Uses apps that ship with Windows 11.</summary>
    public static GroupsConfig Sample { get; } = new(
    [
        new Group("Work", "#3B82F6",
        [
            new AppShortcut("File Explorer", AppsFolder + "Microsoft.Windows.Explorer"),
            new AppShortcut("Notepad", AppsFolder + "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
        ], Icon: "briefcase"),
        new Group("Browsers", "#F97316",
        [
            new AppShortcut("Edge", AppsFolder + "MSEdge"),
        ], Icon: "globe"),
        new Group("Tools", "#22C55E",
        [
            new AppShortcut("Terminal", AppsFolder + "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"),
            new AppShortcut("Calculator", AppsFolder + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
            new AppShortcut("Paint", AppsFolder + "Microsoft.Paint_8wekyb3d8bbwe!App"),
        ], Icon: "wrench"),
    ]);
}
