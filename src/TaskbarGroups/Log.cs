using System.Diagnostics;
using System.IO;

namespace TaskbarGroups;

/// <summary>Minimal append-only log at %LOCALAPPDATA%\TaskbarGroups\taskbargroups.log (overwritten each launch).</summary>
internal static class Log
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarGroups", "taskbargroups.log");

    private static readonly Lock Gate = new();

    public static void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "");
        Info($"Started {Environment.ProcessPath} (pid {Environment.ProcessId})");
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
        Debug.Write(line);
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, line);
        }
        catch (IOException)
        {
            // Logging must never take the app down.
        }
    }
}
