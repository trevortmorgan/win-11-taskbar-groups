using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TaskbarGroups;

/// <summary>Static functions for use in x:Bind expressions.</summary>
public static class BindingHelpers
{
    /// <summary>"#RRGGBB" → brush. Falls back to grey for anything it can't parse.</summary>
    public static SolidColorBrush Brush(string hex) => new(Parse(hex));

    /// <summary>The colour at the given opacity, e.g. for a faint tinted background.</summary>
    public static SolidColorBrush Tint(string hex, double opacity) => new(Parse(hex) with { A = (byte)Math.Round(opacity * 255) });

    /// <summary>The colour mixed 45% towards white, for readable labels on a dark taskbar.</summary>
    public static SolidColorBrush Light(string hex)
    {
        var c = Parse(hex);
        static byte Mix(byte v) => (byte)(v + (255 - v) * 0.45);
        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, Mix(c.R), Mix(c.G), Mix(c.B)));
    }

    /// <summary>First letter of a name. A stand-in until real app icons arrive in milestone 2.</summary>
    public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    private static Color Parse(string hex)
    {
        if (hex is ['#', ..] && hex.Length == 7 && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, null, out var rgb))
            return ColorHelper.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return Colors.Gray;
    }
}
