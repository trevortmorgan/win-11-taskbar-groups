namespace TaskbarGroups.Config;

/// <summary>One app shortcut inside a group. Path may contain environment variables, e.g. %WINDIR%.</summary>
public sealed record AppShortcut(string Name, string Path, string? Arguments = null);

/// <summary>A named, coloured set of app shortcuts shown as one pill on the taskbar.</summary>
public sealed record Group(string Name, string Color, IReadOnlyList<AppShortcut> Apps);

public sealed record GroupsConfig(IReadOnlyList<Group> Groups)
{
    public static GroupsConfig Sample { get; } = new(
    [
        new Group("Work", "#3B82F6",
        [
            new AppShortcut("File Explorer", @"%WINDIR%\explorer.exe"),
            new AppShortcut("Notepad", @"%WINDIR%\System32\notepad.exe"),
        ]),
        new Group("Browsers", "#F97316",
        [
            new AppShortcut("Edge", @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
        ]),
        new Group("Tools", "#22C55E",
        [
            new AppShortcut("Terminal", @"%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe"),
            new AppShortcut("Calculator", @"%WINDIR%\System32\calc.exe"),
        ]),
    ]);
}
