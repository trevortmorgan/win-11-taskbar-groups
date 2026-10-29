using Microsoft.UI.Xaml;
using TaskbarGroups.Config;

namespace TaskbarGroups;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private StripWindow? _strip;
    private TaskbarHost? _host;

    public App()
    {
        InitializeComponent();
        Log.Start();
        UnhandledException += (_, e) => Log.Error("Unhandled exception", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Two strips would sit on top of each other, so only one copy may run.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\TaskbarGroups.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Exit();
            return;
        }

        var config = new ConfigStore(ConfigPathFromArgs() ?? ConfigStore.DefaultPath).Load();
        _strip = new StripWindow(config.Groups);
        _strip.Closed += (_, _) => _host?.Dispose();
        _host = new TaskbarHost(_strip);
    }

    /// <summary>`--config &lt;path&gt;` uses a different groups file (handy for testing layouts).</summary>
    private static string? ConfigPathFromArgs()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "--config");
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
    }
}
