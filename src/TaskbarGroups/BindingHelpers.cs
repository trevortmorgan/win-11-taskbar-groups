using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace TaskbarGroups;

/// <summary>Static functions for use in x:Bind expressions.</summary>
public static class BindingHelpers
{
    /// <summary>"#RRGGBB" → brush. Falls back to grey for anything it can't parse.</summary>
    public static SolidColorBrush Brush(string hex)
    {
        if (hex is ['#', ..] && hex.Length == 7 && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, null, out var rgb))
            return new SolidColorBrush(ColorHelper.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        return new SolidColorBrush(Colors.Gray);
    }
}
