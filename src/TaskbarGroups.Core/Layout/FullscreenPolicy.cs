namespace TaskbarGroups.Layout;

/// <summary>The window the user is working in, as far as the fullscreen decision needs to know.</summary>
/// <param name="Bounds">Its rectangle in physical screen pixels.</param>
/// <param name="Monitor">The rectangle of the monitor it is mostly on.</param>
/// <param name="IsShell">True for the desktop and the taskbars, which can cover a whole monitor without being an app.</param>
public sealed record ForegroundWindow(IntRect Bounds, IntRect Monitor, bool IsShell);

/// <summary>Decides, per monitor, whether a fullscreen app is hiding that monitor's taskbar.</summary>
public static class FullscreenPolicy
{
    /// <summary>
    /// True when a fullscreen app is in front on <paramref name="monitor"/>: the foreground window covers the whole
    /// monitor, or Windows says a fullscreen app / presentation is running machine-wide
    /// (<paramref name="windowsReportsFullscreen"/>) and the foreground window is on this monitor.
    /// A fullscreen app on another monitor doesn't count, since this monitor's taskbar is still showing.
    /// </summary>
    public static bool IsFullscreenOn(IntRect monitor, ForegroundWindow? foreground, bool windowsReportsFullscreen)
    {
        if (foreground is null || foreground.IsShell || monitor.IsEmpty)
            return false;
        return foreground.Bounds.Contains(monitor) || (windowsReportsFullscreen && foreground.Monitor == monitor);
    }
}
