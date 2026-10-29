using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace TaskbarGroups;

internal static class MiddleClick
{
    /// <summary>
    /// A Button only raises Click for the primary button, so watch the pointer for a middle-button release over a tile
    /// and open a new window, like Shift+click. 
    /// </summary>
    public static void Attach(Button button, Action<AppTile> open) =>
        button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, e) =>
        {
            if (button.Tag is not AppTile tile)
                return;
            var point = e.GetCurrentPoint(button);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonReleased)
                return;
            // Released outside the tile means the press was cancelled, as with a normal click.
            if (point.Position.X < 0 || point.Position.Y < 0 || point.Position.X > button.ActualWidth || point.Position.Y > button.ActualHeight)
                return;
            e.Handled = true;
            open(tile);
        }), true);
}
