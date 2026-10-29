using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TaskbarGroups.Config;

namespace TaskbarGroups;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private ConfigStore? _store;
    private StripWindow? _strip;
    private TaskbarHost? _host;
    private FileSystemWatcher? _configWatcher;
    private DispatcherQueueTimer? _reloadTimer;

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

        _store = new ConfigStore(ConfigPathFromArgs() ?? ConfigStore.DefaultPath);
        _strip = new StripWindow();
        _strip.Closed += (_, _) => _host?.Dispose();
        _host = new TaskbarHost(_strip);

        _ = LoadGroupsAsync();
        WatchConfigFile();
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var config = _store!.LoadOrCreate();
            await _strip!.SetGroupsAsync(config.Groups);
            Log.Info($"Loaded {config.Groups.Count} groups from {_store.Path}");
        }
        catch (Exception ex)
        {
            // Usually a typo while hand-editing the JSON. Keep showing what we had.
            Log.Error($"Couldn't load {_store!.Path}; keeping the current groups", ex);
        }
    }

    /// <summary>Reloads the groups whenever the config file is saved, so it can be edited by hand.</summary>
    private void WatchConfigFile()
    {
        var directory = Path.GetDirectoryName(_store!.Path)!;
        Directory.CreateDirectory(directory);

        // Editors often save in several steps, so wait for things to settle before reloading.
        _reloadTimer = _strip!.DispatcherQueue.CreateTimer();
        _reloadTimer.Interval = TimeSpan.FromMilliseconds(400);
        _reloadTimer.IsRepeating = false;
        _reloadTimer.Tick += (_, _) => _ = LoadGroupsAsync();

        _configWatcher = new FileSystemWatcher(directory, Path.GetFileName(_store.Path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        FileSystemEventHandler onChange = (_, _) => _strip.DispatcherQueue.TryEnqueue(() =>
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
        _configWatcher.Changed += onChange;
        _configWatcher.Created += onChange;
        _configWatcher.Renamed += (s, e) => onChange(s, e);
        _configWatcher.EnableRaisingEvents = true;
    }

    /// <summary>`--config &lt;path&gt;` uses a different groups file (handy for testing layouts).</summary>
    private static string? ConfigPathFromArgs()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "--config");
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
    }
}
