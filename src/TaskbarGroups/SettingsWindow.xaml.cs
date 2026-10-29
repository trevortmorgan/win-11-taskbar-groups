using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TaskbarGroups.Config;
using TaskbarGroups.Icons;
using TaskbarGroups.Shell;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;

namespace TaskbarGroups;

/// <summary>
/// Where groups are created and edited. Works on its own copy of the config and saves it after every change
/// (shortly after, while typing); the strip picks the change up from the file.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>The colours used in the mockup (Tailwind 500s), in the order they're offered.</summary>
    private static readonly string[] Palette =
        ["#3B82F6", "#F97316", "#22C55E", "#A855F7", "#EC4899", "#EF4444", "#EAB308", "#14B8A6"];

    private readonly ConfigStore _store;
    private readonly string _configDirectory;
    private readonly ObservableCollection<EditableGroup> _groups = [];
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly nint _handle;
    private List<IconChoice>? _iconChoices;

    /// <summary>True while the controls are being filled in from the selected group, so that isn't mistaken for an edit.</summary>
    private bool _loading;

    /// <param name="loadError">Why the config couldn't be read. The editor stays disabled so a file with a typo isn't overwritten.</param>
    public SettingsWindow(ConfigStore store, GroupsConfig? config, string? loadError)
    {
        InitializeComponent();
        _store = store;
        _configDirectory = Path.GetDirectoryName(store.Path)!;

        SystemBackdrop = new MicaBackdrop();
        _handle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = WindowChrome.GetDpi(_handle) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(980 * scale), (int)(720 * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsMaximizable = false;

        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => Save();
        Closed += (_, _) => FlushSave();

        BuildSwatches();

        if (loadError is not null || config is null)
        {
            ErrorBar.Title = "Can't open your groups";
            ErrorBar.Message = $"{store.Path} couldn't be read ({loadError}). Fix or delete the file, then open settings again.";
            ErrorBar.IsOpen = true;
            Editor.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var group in config.Groups)
            AddToList(new EditableGroup(group, _configDirectory));
        _groups.CollectionChanged += OnGroupsChanged;
        GroupList.ItemsSource = _groups;
        AppList.ItemsSource = null;
        Root.Loaded += (_, _) =>
        {
            GroupList.SelectedIndex = _groups.Count > 0 ? 0 : -1;
            _ = LoadAppIconsAsync(_groups.SelectMany(g => g.Apps));
        };
        UpdateSelection();
    }

    private EditableGroup? Selected => GroupList.SelectedItem as EditableGroup;

    // ----- Saving -----

    private void OnEdited(object? sender, EventArgs e) => _saveTimer.Debounce();

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (EditableGroup group in e.NewItems ?? Array.Empty<EditableGroup>())
            group.Changed += OnEdited;
        foreach (EditableGroup group in e.OldItems ?? Array.Empty<EditableGroup>())
            group.Changed -= OnEdited;
        _saveTimer.Debounce();
    }

    private void AddToList(EditableGroup group)
    {
        _groups.Add(group);
        group.Changed += OnEdited;
    }

    private void FlushSave()
    {
        if (_saveTimer.IsRunning)
        {
            _saveTimer.Stop();
            Save();
        }
    }

    private void Save()
    {
        try
        {
            _store.Save(new GroupsConfig([.. _groups.Select(g => g.ToGroup())]));
            ErrorBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't save {_store.Path}", ex);
            ErrorBar.Title = "Couldn't save your changes";
            ErrorBar.Message = ex.Message;
            ErrorBar.IsOpen = true;
        }
    }

    // ----- Groups -----

    private void OnGroupSelected(object sender, SelectionChangedEventArgs e) => UpdateSelection();

    private void UpdateSelection()
    {
        var group = Selected;
        Detail.Visibility = group is null ? Visibility.Collapsed : Visibility.Visible;
        NoSelection.Visibility = group is null ? Visibility.Visible : Visibility.Collapsed;
        if (group is null)
            return;

        _loading = true;
        NameBox.Text = group.Name;
        AppList.ItemsSource = group.Apps;
        _loading = false;
        RefreshColor();
        RefreshIcon();
        RefreshAppsEmpty();
    }

    private void OnAddGroup(object sender, RoutedEventArgs e)
    {
        var color = Palette.FirstOrDefault(c => !_groups.Any(g => g.Color == c)) ?? Palette[_groups.Count % Palette.Length];
        var group = new EditableGroup(new Group("New group", color, []), _configDirectory);
        _groups.Add(group);
        GroupList.SelectedItem = group;
        NameBox.Focus(FocusState.Programmatic);
        NameBox.SelectAll();
    }

    private void OnMoveGroupUp(object sender, RoutedEventArgs e) => MoveSelected(-1);

    private void OnMoveGroupDown(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        var index = GroupList.SelectedIndex;
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _groups.Count)
            return;
        var group = _groups[index];
        _groups.Move(index, target);
        GroupList.SelectedItem = group;
    }

    private async void OnDeleteGroup(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } group)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = $"Delete \"{group.Name}\"?",
            Content = "The group and its list of apps are removed. The apps themselves aren't touched.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var index = _groups.IndexOf(group);
        _groups.Remove(group);
        GroupList.SelectedIndex = Math.Min(index, _groups.Count - 1);
        UpdateSelection();
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading && Selected is { } group)
            group.Name = NameBox.Text;
    }

    // ----- Colour -----

    private void BuildSwatches()
    {
        foreach (var hex in Palette)
        {
            var swatch = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(16),
                Tag = hex,
                Content = new Ellipse { Width = 20, Height = 20, Fill = BindingHelpers.Brush(hex) },
            };
            ToolTipService.SetToolTip(swatch, hex);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(swatch, $"Colour {hex}");
            swatch.Click += (_, _) =>
            {
                if (Selected is { } group)
                {
                    group.Color = hex;
                    RefreshColor();
                    RefreshIcon();
                }
            };
            Swatches.Children.Add(swatch);
        }
    }

    /// <summary>Rings the chosen swatch (or the custom button, if the colour isn't one of them).</summary>
    private void RefreshColor()
    {
        if (Selected is not { } group)
            return;

        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var none = new SolidColorBrush(Colors.Transparent);
        var isPalette = false;
        foreach (Button swatch in Swatches.Children)
        {
            var selected = string.Equals((string)swatch.Tag, group.Color, StringComparison.OrdinalIgnoreCase);
            isPalette |= selected;
            swatch.BorderBrush = selected ? accent : none;
            swatch.BorderThickness = new Thickness(selected ? 2 : 0);
        }
        CustomColorButton.BorderBrush = isPalette ? none : accent;
        CustomColorButton.BorderThickness = new Thickness(isPalette ? 0 : 2);

        _loading = true;
        Picker.Color = ParseColor(group.Color);
        _loading = false;
    }

    private void OnPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_loading || Selected is not { } group)
            return;
        var c = args.NewColor;
        group.Color = string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
        RefreshColor();
        RefreshIcon();
    }

    private static Color ParseColor(string hex) =>
        BindingHelpers.Brush(hex).Color;

    // ----- Icon -----

    private void RefreshIcon()
    {
        if (Selected is not { } group)
            return;
        IconPreview.Icon = group.ResolvedIcon;
        IconPreview.Color = group.Color;
        IconLabel.Text = string.IsNullOrWhiteSpace(group.Icon) ? "No icon"
            : group.Icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(group.Icon)
            : group.Icon;
        if (_iconChoices is not null)
        {
            foreach (var choice in _iconChoices)
                choice.Color = group.Color;
        }
    }

    private void OnIconFlyoutOpening(object? sender, object e)
    {
        if (_iconChoices is null)
        {
            _iconChoices = [];
            foreach (var name in GroupIcon.BundledNames)
            {
                if (GroupIcon.TryResolve(name, _configDirectory, out var icon, out _))
                    _iconChoices.Add(new IconChoice(name, icon, Selected?.Color ?? Palette[0]));
            }
        }
        foreach (var choice in _iconChoices)
            choice.Color = Selected?.Color ?? Palette[0];
        IconSearch.Text = "";
        IconGrid.ItemsSource = _iconChoices;
    }

    private void OnIconSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_iconChoices is null)
            return;
        var query = GroupIcon.NormalizeName(sender.Text);
        IconGrid.ItemsSource = query.Length == 0 ? _iconChoices : _iconChoices.Where(c => c.Name.Contains(query)).ToList();
    }

    private void OnIconClick(object sender, ItemClickEventArgs e)
    {
        if (Selected is { } group && e.ClickedItem is IconChoice choice)
            group.Icon = choice.Name;
        RefreshIcon();
        IconFlyout.Hide();
    }

    private void OnNoIcon(object sender, RoutedEventArgs e)
    {
        if (Selected is { } group)
            group.Icon = null;
        RefreshIcon();
        IconFlyout.Hide();
    }

    private async void OnBrowseSvg(object sender, RoutedEventArgs e)
    {
        IconFlyout.Hide();
        var picker = NewPicker([".svg"]);
        var file = await picker.PickSingleFileAsync();
        if (file is null || Selected is not { } group)
            return;

        // Stored as written, so a file next to groups.json can be given as a short relative path.
        var relative = Path.GetRelativePath(_configDirectory, file.Path);
        group.Icon = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? file.Path : relative;
        RefreshIcon();
        if (group.ResolvedIcon is null)
        {
            group.Icon = null;
            RefreshIcon();
            await ShowMessageAsync("Can't use that icon", $"{Path.GetFileName(file.Path)} isn't a usable SVG (it may be missing, too big, or not an SVG file).");
        }
    }

    // ----- Apps -----

    private void RefreshAppsEmpty() =>
        AppsEmpty.Visibility = Selected is { Apps.Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;

    private void OnRemoveApp(object sender, RoutedEventArgs e)
    {
        if (Selected is { } group && (sender as FrameworkElement)?.Tag is EditableApp app)
        {
            group.Apps.Remove(app);
            RefreshAppsEmpty();
        }
    }

    private void AddApps(IEnumerable<AppShortcut> shortcuts)
    {
        if (Selected is not { } group)
            return;
        var added = new List<EditableApp>();
        foreach (var shortcut in shortcuts)
        {
            var app = new EditableApp(shortcut);
            group.Apps.Add(app);
            added.Add(app);
        }
        RefreshAppsEmpty();
        _ = LoadAppIconsAsync(added);
    }

    private async Task LoadAppIconsAsync(IEnumerable<EditableApp> apps)
    {
        var list = apps.ToList();
        if (list.Count == 0 || Root.XamlRoot is null)
            return;

        var scale = Root.XamlRoot.RasterizationScale;
        var icons = await IconLoader.LoadAsync(list.Select(a => Environment.ExpandEnvironmentVariables(a.Path)), (int)Math.Round(24 * scale));
        foreach (var app in list)
        {
            if (icons.GetValueOrDefault(Environment.ExpandEnvironmentVariables(app.Path)) is { } pixels)
                app.Icon = IconLoader.ToBitmap(pixels);
        }
    }

    private async void OnBrowseApp(object sender, RoutedEventArgs e)
    {
        var picker = NewPicker([".exe", ".lnk", ".url", ".bat", ".cmd"]);
        var files = await picker.PickMultipleFilesAsync();
        if (files is { Count: > 0 })
            AddApps(files.Select(f => AppDiscovery.FromPath(f.Path)));
    }

    private static bool HasFiles(DataPackageView view) => view.Contains(StandardDataFormats.StorageItems);

    private void OnAppsDragOver(object sender, DragEventArgs e)
    {
        // Reordering the list internally isn't a drop of files, so let the list handle that itself.
        if (!HasFiles(e.DataView))
            return;
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Add to group";
    }

    private async void OnAppsDrop(object sender, DragEventArgs e)
    {
        if (!HasFiles(e.DataView))
            return;
        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            AddApps(items.Where(i => !string.IsNullOrEmpty(i.Path)).Select(i => AppDiscovery.FromPath(i.Path)));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnPickInstalled(object sender, RoutedEventArgs e)
    {
        var search = new AutoSuggestBox { PlaceholderText = "Search apps", QueryIcon = new SymbolIcon(Symbol.Find) };
        var list = new ListView { SelectionMode = ListViewSelectionMode.Multiple, Height = 360, DisplayMemberPath = nameof(InstalledAppChoice.Name) };
        var status = new TextBlock { Text = "Loading your apps…", Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        var panel = new StackPanel { Spacing = 8, Width = 420 };
        panel.Children.Add(search);
        panel.Children.Add(status);
        panel.Children.Add(list);

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Installed apps",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        List<InstalledAppChoice> all = [];
        var selected = new HashSet<InstalledAppChoice>();
        list.SelectionChanged += (_, args) =>
        {
            foreach (InstalledAppChoice c in args.AddedItems)
                selected.Add(c);
            foreach (InstalledAppChoice c in args.RemovedItems)
                selected.Remove(c);
            dialog.IsPrimaryButtonEnabled = selected.Count > 0;
        };
        search.TextChanged += (box, _) =>
        {
            var query = box.Text.Trim();
            list.ItemsSource = query.Length == 0 ? all : all.Where(a => a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
            // Keep earlier picks ticked while the list is filtered.
            foreach (var item in list.Items.OfType<InstalledAppChoice>().Where(selected.Contains))
                list.SelectedItems.Add(item);
        };

        _ = LoadInstalledAppsAsync().ContinueWith(task =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (task.IsCompletedSuccessfully && task.Result.Count > 0)
                {
                    all = [.. task.Result.Select(a => new InstalledAppChoice(a))];
                    list.ItemsSource = all;
                    status.Visibility = Visibility.Collapsed;
                }
                else
                {
                    status.Text = "Couldn't list your installed apps. Use Browse instead.";
                }
            });
        });

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            AddApps(selected.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).Select(c => c.App.ToShortcut()));
    }

    /// <summary>Asks PowerShell for <c>Get-StartApps</c>, the same list the Start menu shows (including Store apps).</summary>
    private static async Task<IReadOnlyList<StartApp>> LoadInstalledAppsAsync()
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", "[Console]::OutputEncoding=[Text.Encoding]::UTF8; Get-StartApps | ConvertTo-Json -Compress" })
            start.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(start)!;
            var json = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return AppDiscovery.ParseStartApps(json);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't list installed apps", ex);
            return [];
        }
    }

    // ----- Helpers -----

    private FileOpenPicker NewPicker(IEnumerable<string> extensions)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        foreach (var extension in extensions)
            picker.FileTypeFilter.Add(extension);
        if (extensions.Contains(".exe"))
            picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
        return picker;
    }

    private async Task ShowMessageAsync(string title, string message) =>
        await new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = message, CloseButtonText = "OK" }.ShowAsync();
}

internal static class TimerExtensions
{
    /// <summary>Starts the timer over, so it fires once things have gone quiet.</summary>
    public static void Debounce(this DispatcherQueueTimer timer)
    {
        timer.Stop();
        timer.Start();
    }
}
