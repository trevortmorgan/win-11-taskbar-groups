using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskbarGroups.Shell;

namespace TaskbarGroups;

/// <summary>Loads app icons off the UI thread and turns them into XAML bitmaps.</summary>
internal static class IconLoader
{
    /// <summary>
    /// Loads icons for every distinct path on a background STA thread (some Shell icon handlers require STA).
    /// Paths with no icon map to null.
    /// </summary>
    public static Task<IReadOnlyDictionary<string, IconPixels?>> LoadAsync(IEnumerable<string> parsingNames, int sizePx)
    {
        var names = parsingNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var done = new TaskCompletionSource<IReadOnlyDictionary<string, IconPixels?>>();

        var thread = new Thread(() =>
        {
            var icons = new Dictionary<string, IconPixels?>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                try
                {
                    icons[name] = ShellIcons.Load(name, sizePx);
                    if (icons[name] is null)
                        Log.Info($"No icon found for {name}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Loading icon for {name} failed", ex);
                    icons[name] = null;
                }
            }
            done.SetResult(icons);
        })
        {
            IsBackground = true,
            Name = "Icon loader",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    /// <summary>Must be called on the UI thread.</summary>
    public static WriteableBitmap ToBitmap(IconPixels icon)
    {
        var bitmap = new WriteableBitmap(icon.Width, icon.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
            stream.Write(icon.Bgra);
        bitmap.Invalidate();
        return bitmap;
    }
}
