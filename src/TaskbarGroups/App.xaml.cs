using System.Diagnostics;
using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TaskbarGroups.Config;
using TaskbarGroups.Shell;
using TaskbarGroups.Startup;

namespace TaskbarGroups;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private ConfigStore? _store;
    private StripWindow? _strip;
    private TaskbarHost? _host;
    private FileSystemWatcher? _configWatcher;
    private DispatcherQueueTimer? _reloadTimer;
    private SettingsWindow? _settings;
    private TrayIcon? _tray;
    private readonly StartupRegistration _startup = StartupRegistration.ForCurrentProcess();

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
        _strip.Closed += (_, _) =>
        {
            _tray?.Dispose();
            _host?.Dispose();
        };
        _host = new TaskbarHost(_strip);

        _ = LoadGroupsAsync();
        WatchConfigFile();
        CreateTrayIcon();
    }

    /// <summary>The notification-area icon: the way back in when the strip is hidden (no room, fullscreen app).</summary>
    private void CreateTrayIcon()
    {
        try
        {
            _tray = new TrayIcon("Taskbar Groups", Environment.ProcessPath, BuildTrayMenu);
            _tray.Selected += ShowSettings;
        }
        catch (Exception ex)
        {
            // The strip works without it, so don't refuse to start.
            Log.Error("Couldn't add the tray icon", ex);
        }
    }

    private IReadOnlyList<TrayMenuItem> BuildTrayMenu() =>
    [
        new("Settings", ShowSettings),
        new("Show on taskbar", () => _host!.UserHidden = !_host.UserHidden, Checked: !_host!.UserHidden),
        new("Start with Windows", ToggleStartup, Checked: IsStartupEnabled()),
        TrayMenuItem.Separator,
        new("Edit groups file", () => OpenShell(_store!.Path)),
        new("Open log folder", () => OpenShell(Log.Directory)),
        TrayMenuItem.Separator,
        new("Exit", Exit),
    ];

    private bool IsStartupEnabled()
    {
        try
        {
            return _startup.IsEnabled; // read fresh every time the menu opens
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't read the start-with-Windows setting", ex);
            return false;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            var enable = !_startup.IsEnabled;
            _startup.SetEnabled(enable);
            Log.Info($"Start with Windows: {(enable ? "on" : "off")}");
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't change the start-with-Windows setting", ex);
        }
    }

    private static void OpenShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't open {path}", ex);
        }
    }

    /// <summary>Opens the settings window, or brings it forward if it's already open.</summary>
    public void ShowSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        GroupsConfig? config = null;
        string? error = null;
        try
        {
            config = _store!.Load();
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't load {_store!.Path} for the settings window", ex);
            error = ex.Message;
        }

        _settings = new SettingsWindow(_store!, config, error);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Activate();
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var config = _store!.LoadOrCreate();
            await _strip!.SetGroupsAsync(config.Groups, Path.GetDirectoryName(_store.Path)!);
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
