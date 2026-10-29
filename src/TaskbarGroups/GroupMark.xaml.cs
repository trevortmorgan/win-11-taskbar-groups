using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskbarGroups.Icons;
using Windows.Storage.Streams;

namespace TaskbarGroups;

/// <summary>
/// Marks a group with its icon in the group's colour, or a coloured dot if it has no icon (or the icon can't be drawn).
/// Usage: <c>&lt;local:GroupMark Icon="{x:Bind Icon}" Color="{x:Bind Color}" /&gt;</c> with a <see cref="GroupView"/>.
/// </summary>
public sealed partial class GroupMark : UserControl
{
    /// <summary>Icon size: the same as the small glyphs on Windows' own taskbar.</summary>
    public const double IconDips = 16;

    /// <summary>A <see cref="GroupIcon"/>, or null for the dot. (Typed as object because XAML can't register .NET classes.)</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(object), typeof(GroupMark), new PropertyMetadata(null, OnChanged));

    /// <summary>"#RRGGBB".</summary>
    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(nameof(Color), typeof(string), typeof(GroupMark), new PropertyMetadata("", OnChanged));

    private int _version;

    public GroupMark()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            XamlRoot.Changed += OnXamlRootChanged;
            Update();
        };
        Unloaded += (_, _) =>
        {
            if (XamlRoot is not null)
                XamlRoot.Changed -= OnXamlRootChanged;
        };
    }

    public GroupIcon? Icon
    {
        get => (GroupIcon?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GroupMark)d).Update();

    // Moving to a monitor with a different scale needs a re-render at the new pixel size.
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Update();

    private async void Update()
    {
        Dot.Fill = BindingHelpers.Brush(Color);
        var version = ++_version;
        if (Icon is not { } icon || XamlRoot is null)
        {
            ShowGlyph(false);
            return;
        }

        // Like the app icons: render at exactly the screen pixels it'll cover, and display it at that size in DIPs,
        // so nothing gets rescaled (which is what makes icons look soft).
        var scale = XamlRoot.RasterizationScale;
        var sizePx = (int)Math.Round(IconDips * scale);
        try
        {
            var source = new SvgImageSource { RasterizePixelWidth = sizePx, RasterizePixelHeight = sizePx };
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteString(icon.ToSvg(sizePx, Color));
                await writer.StoreAsync();
                writer.DetachStream();
            }
            stream.Seek(0);
            var status = await source.SetSourceAsync(stream);
            if (version != _version)
                return; // Superseded by a newer update while loading.
            if (status != SvgImageSourceLoadStatus.Success)
            {
                Log.Error($"Couldn't draw a group icon ({status}); showing the dot");
                ShowGlyph(false);
                return;
            }
            Glyph.Source = source;
            Glyph.Width = Glyph.Height = sizePx / scale;
            ShowGlyph(true);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't draw a group icon; showing the dot", ex);
            if (version == _version)
                ShowGlyph(false);
        }
    }

    private void ShowGlyph(bool show)
    {
        Glyph.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        Dot.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }
}
