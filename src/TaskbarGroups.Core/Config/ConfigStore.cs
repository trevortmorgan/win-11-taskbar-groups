using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarGroups.Config;

/// <summary>Loads and saves <see cref="GroupsConfig"/> as JSON (default: %APPDATA%\TaskbarGroups\groups.json).</summary>
public sealed class ConfigStore(string path)
{
    public static string DefaultPath { get; } =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarGroups", "groups.json");

    public string Path { get; } = path;

    /// <summary>Returns the saved config, or the sample config if no file exists yet.</summary>
    public GroupsConfig Load()
    {
        if (!File.Exists(Path))
            return GroupsConfig.Sample;

        using var stream = File.OpenRead(Path);
        return JsonSerializer.Deserialize(stream, ConfigJsonContext.Default.GroupsConfig)
            ?? throw new InvalidDataException($"Config file is empty: {Path}");
    }

    public void Save(GroupsConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        // Write to a temp file then swap, so a crash mid-write can't corrupt the user's groups.
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, ConfigJsonContext.Default.GroupsConfig));
        File.Move(temp, Path, overwrite: true);
    }
}

// Source-generated serializer: no reflection, so it survives trimming in the release build.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(GroupsConfig))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
