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

    /// <summary>The colour mixed 45% towards white on a dark taskbar (black on a light one), for readable labels.</summary>
    public static SolidColorBrush Light(string hex)
    {
        var c = Parse(hex);
        var target = TaskbarTheme.Color("TgLabelMixTarget");
        static byte Mix(byte v, byte t) => (byte)(v + (t - v) * 0.45);
        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, Mix(c.R, target.R), Mix(c.G, target.G), Mix(c.B, target.B)));
    }

    private static Color Parse(string hex)
    {
        if (hex is ['#', ..] && hex.Length == 7 && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, null, out var rgb))
            return ColorHelper.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return Colors.Gray;
    }
}
