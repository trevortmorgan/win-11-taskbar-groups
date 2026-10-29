using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarGroups.Config;

/// <summary>An app in the Start menu, as listed by <c>Get-StartApps</c>.</summary>
public sealed record StartApp(string Name, string AppId)
{
    public AppShortcut ToShortcut() => new(Name, AppDiscovery.AppsFolder + AppId);
}

/// <summary>Turns the things a user picks (a file, a Start menu app) into <see cref="AppShortcut"/>s.</summary>
public static class AppDiscovery
{
    public const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>A file or folder the user browsed to or dropped in. Shortcuts and programs are named after their file.</summary>
    public static AppShortcut FromPath(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var name = Path.GetFileNameWithoutExtension(trimmed);
        if (name.Length == 0)
            name = Path.GetFileName(trimmed);
        return new AppShortcut(name.Length == 0 ? path : name, path);
    }

    /// <summary>
    /// Reads the JSON from <c>Get-StartApps | ConvertTo-Json</c>: an array, or a bare object when there's only one app.
    /// Entries without a name or ID are skipped; the result is sorted by name.
    /// </summary>
    public static IReadOnlyList<StartApp> ParseStartApps(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement];

        return
        [
            .. elements
                .Select(e => e.Deserialize(StartAppJsonContext.Default.StartAppDto))
                .OfType<StartAppDto>()
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.AppId))
                .Select(a => new StartApp(a.Name!.Trim(), a.AppId!.Trim()))
                .DistinctBy(a => a.AppId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }
}

internal sealed record StartAppDto(string? Name, string? AppId);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StartAppDto))]
internal sealed partial class StartAppJsonContext : JsonSerializerContext;
