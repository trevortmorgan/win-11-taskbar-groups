using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TaskbarGroups.Icons;

/// <summary>
/// The icon on a group's pill: one of the bundled Fluent UI System Icons by name (e.g. "briefcase"), or the
/// user's own .svg file (e.g. one saved from icones.js.org). <see cref="ToSvg"/> gives SVG markup for an exact
/// pixel size, coloured with the group's colour.
/// </summary>
public sealed partial class GroupIcon
{
    /// <summary>Custom .svg files bigger than this are refused; icons are a few KB.</summary>
    public const int MaxFileBytes = 256 * 1024;

    // Fluent icons are drawn in this colour; we swap it for the group's.
    private const string FluentInk = "#212121";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>>> Bundled = new(FindBundled);

    // Design size in px (0 for a custom file) → SVG markup.
    private readonly IReadOnlyDictionary<int, string> _variants;
    private readonly bool _isBundled;

    private GroupIcon(IReadOnlyDictionary<int, string> variants, bool isBundled)
    {
        _variants = variants;
        _isBundled = isBundled;
    }

    /// <summary>
    /// Names of the bundled Fluent icons, sorted. They match the names in the "Fluent" collection on icones.js.org,
    /// without the size and style (e.g. "music-note-2" for "music-note-2-20-regular").
    /// </summary>
    public static IReadOnlyList<string> BundledNames { get; } = [.. Bundled.Value.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Finds the icon a group's <c>icon</c> setting refers to: a bundled name, or a path ending in .svg (environment
    /// variables are expanded; relative paths are relative to <paramref name="baseDirectory"/>, the config file's folder).
    /// Returns false with a null <paramref name="error"/> when no icon is set, or with a reason when it can't be used.
    /// </summary>
    public static bool TryResolve(string? icon, string baseDirectory,
        [NotNullWhen(true)] out GroupIcon? result, out string? error)
    {
        result = null;
        error = null;
        if (string.IsNullOrWhiteSpace(icon))
            return false;

        if (icon.Trim().EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return TryLoadFile(icon.Trim(), baseDirectory, out result, out error);

        if (!Bundled.Value.TryGetValue(NormalizeName(icon), out var resources))
        {
            error = $"there's no bundled icon called \"{icon}\"";
            return false;
        }
        result = new GroupIcon(resources.ToDictionary(r => r.Key, r => ReadResource(r.Value)), isBundled: true);
        return true;
    }

    /// <summary>"Music Note 2", "music_note_2" → "music-note-2".</summary>
    public static string NormalizeName(string name) =>
        string.Join('-', name.Trim().ToLowerInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// SVG markup that draws this icon at <paramref name="pixelSize"/> × <paramref name="pixelSize"/> pixels in the
    /// given "#RRGGBB" colour. Single-colour icons (black, or <c>currentColor</c>) take the colour; multi-colour
    /// ones keep their own colours.
    /// </summary>
    public string ToSvg(int pixelSize, string colorHex)
    {
        var markup = _variants[PickDesignSize(_variants.Keys, pixelSize)];
        return Recolor(markup, pixelSize, colorHex, replaceFluentInk: _isBundled);
    }

    /// <summary>
    /// Fluent draws each icon separately for 16, 20 and 24 px, so the one drawn for the exact pixel size is the
    /// sharpest. Otherwise prefer one that scales by a whole number (16 → 32 px), then the next size up.
    /// </summary>
    public static int PickDesignSize(IEnumerable<int> designSizes, int pixelSize)
    {
        var sizes = designSizes.Order().ToList();
        if (sizes.Contains(pixelSize))
            return pixelSize;
        var whole = sizes.LastOrDefault(s => s > 0 && pixelSize % s == 0);
        return whole > 0 ? whole : sizes.FirstOrDefault(s => s > pixelSize, sizes[^1]);
    }

    private static bool TryLoadFile(string icon, string baseDirectory, out GroupIcon? result, out string? error)
    {
        result = null;
        error = null;
        string path;
        try
        {
            path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(icon), baseDirectory);
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                error = $"icon file not found: {path}";
                return false;
            }
            if (file.Length > MaxFileBytes)
            {
                error = $"icon file is bigger than {MaxFileBytes / 1024} KB: {path}";
                return false;
            }
            var markup = File.ReadAllText(path);
            Parse(markup); // Fail now, with a useful message, rather than when it's drawn.
            result = new GroupIcon(new Dictionary<int, string> { [0] = markup }, isBundled: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
                                       or XmlException or InvalidDataException)
        {
            error = $"can't read icon file {icon}: {ex.Message}";
            return false;
        }
    }

    private static string Recolor(string markup, int pixelSize, string colorHex, bool replaceFluentInk)
    {
        var svg = Parse(markup);
        foreach (var attribute in svg.DescendantsAndSelf().Attributes())
        {
            var value = CurrentColor().Replace(attribute.Value, colorHex);
            if (replaceFluentInk)
                value = value.Replace(FluentInk, colorHex, StringComparison.OrdinalIgnoreCase);
            attribute.Value = value;
        }

        // Shapes with no fill are drawn black, which would vanish on the dark taskbar.
        if (svg.Attribute("fill") is null)
            svg.SetAttributeValue("fill", colorHex);

        // Size it to the exact pixels it'll be drawn at. The viewBox keeps the drawing's own coordinates.
        if (svg.Attribute("viewBox") is null
            && TryParseLength(svg.Attribute("width")?.Value, out var width)
            && TryParseLength(svg.Attribute("height")?.Value, out var height))
        {
            svg.SetAttributeValue("viewBox", FormattableString.Invariant($"0 0 {width} {height}"));
        }
        svg.SetAttributeValue("width", pixelSize.ToString(CultureInfo.InvariantCulture));
        svg.SetAttributeValue("height", pixelSize.ToString(CultureInfo.InvariantCulture));

        return svg.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Parse(string markup)
    {
        // No DTDs: a hand-picked file shouldn't be able to expand entities or reach out for external ones.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(markup), settings);
        var root = XDocument.Load(reader).Root;
        if (root?.Name.LocalName != "svg")
            throw new InvalidDataException("not an SVG file");
        return root;
    }

    private static bool TryParseLength(string? value, out double length) =>
        double.TryParse(value?.Trim().TrimEnd('x', 'p'), NumberStyles.Float, CultureInfo.InvariantCulture, out length);

    // Name → design size → resource name, e.g. "briefcase" → 16 → "Fluent/ic_fluent_briefcase_16_regular.svg".
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> FindBundled() =>
        typeof(GroupIcon).Assembly.GetManifestResourceNames()
            .Select(r => (Resource: r, Match: FluentFileName().Match(r)))
            .Where(x => x.Match.Success)
            .GroupBy(x => NormalizeName(x.Match.Groups["name"].Value))
            .ToDictionary(
                g => g.Key,
                IReadOnlyDictionary<int, string> (g) => g.ToDictionary(
                    x => int.Parse(x.Match.Groups["size"].Value, CultureInfo.InvariantCulture), x => x.Resource));

    private static string ReadResource(string name)
    {
        using var stream = typeof(GroupIcon).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^Fluent/ic_fluent_(?<name>.+)_(?<size>\d+)_regular\.svg$")]
    private static partial Regex FluentFileName();

    [GeneratedRegex("currentColor", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentColor();
}
