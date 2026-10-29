using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TaskbarGroups.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TaskbarGroups.Shell;

/// <summary>Win32 tweaks that turn an ordinary app window into a borderless, always-on-top taskbar overlay.</summary>
public static class WindowChrome
{
    private static readonly HWND HwndTopmost = (HWND)(nint)(-1);

    /// <summary>
    /// No taskbar/Alt+Tab entry, never takes focus when clicked, square corners, and a see-through background
    /// (XAML content still draws; anything we don't paint shows the taskbar underneath).
    /// </summary>
    public static void MakeOverlay(nint hwnd) => MakeFrameless(hwnd, activatable: false);

    /// <summary>
    /// The same as <see cref="MakeOverlay"/>, except the window can take focus (and so get keyboard input), for the
    /// Groups flyout.
    /// </summary>
    public static void MakePopup(nint hwnd) => MakeFrameless(hwnd, activatable: true);

    private static unsafe void MakeFrameless(nint hwnd, bool activatable)
    {
        var h = (HWND)hwnd;

        // WinUI keeps (and re-applies) a dialog frame even with border and title bar turned off, which draws a 3px
        // edge. Changing the style doesn't stick, so instead tell Windows the frame has zero size.
        PInvoke.SetWindowSubclass(h, &FramelessWindowProc, FramelessSubclassId, 0);

        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle = (exStyle | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) & ~WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        exStyle = activatable ? exStyle & ~WINDOW_EX_STYLE.WS_EX_NOACTIVATE : exStyle | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        PInvoke.SetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);

        var corners = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
        PInvoke.DwmSetWindowAttribute(h, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &corners, sizeof(DWM_WINDOW_CORNER_PREFERENCE));

        // Blur-behind with an empty region makes DWM honour per-pixel alpha without actually blurring anything.
        var region = PInvoke.CreateRectRgn(-2, -2, -1, -1);
        try
        {
            PInvoke.DwmEnableBlurBehindWindow(h, new DWM_BLURBEHIND
            {
                dwFlags = PInvoke.DWM_BB_ENABLE | PInvoke.DWM_BB_BLURREGION,
                fEnable = true,
                hRgnBlur = region,
            });
        }
        finally
        {
            PInvoke.DeleteObject(region);
        }

        // Make Windows recalculate the frame now, so the zero-size frame and style changes take effect.
        PInvoke.SetWindowPos(h, HWND.Null, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    private const nuint FramelessSubclassId = 0x7467; // arbitrary, unique per window

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT FramelessWindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam, nuint id, nuint data) =>
        msg == PInvoke.WM_NCCALCSIZE && wParam.Value != 0
            ? (LRESULT)0 // the whole window is client area: no frame
            : PInvoke.DefSubclassProc(hwnd, msg, wParam, lParam);

    /// <summary>Moves/resizes the window and puts it on top of the taskbar, without activating it.</summary>
    public static void PlaceOnTop(nint hwnd, IntRect rect) =>
        PInvoke.SetWindowPos((HWND)hwnd, HwndTopmost, rect.Left, rect.Top, rect.Width, rect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    /// <summary>
    /// Re-raises the window above the taskbar. Both are "topmost"; clicking the taskbar brings it to the front
    /// of the topmost band, so we have to step back in front of it.
    /// </summary>
    public static void BringToTop(nint hwnd) =>
        PInvoke.SetWindowPos((HWND)hwnd, HwndTopmost, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    /// <summary>The window the user is working in (has keyboard focus), or 0.</summary>
    public static nint GetForeground() => PInvoke.GetForegroundWindow();

    /// <summary>Gives a window keyboard focus back. Only allowed while our process has the focus.</summary>
    public static void SetForeground(nint hwnd) => PInvoke.SetForegroundWindow((HWND)hwnd);

    /// <summary>Brings the window to the front and gives it keyboard focus.</summary>
    public static void Activate(nint hwnd)
    {
        BringToTop(hwnd);
        PInvoke.SetForegroundWindow((HWND)hwnd);
    }

    /// <summary>DPI of the monitor the window is on (96 = 100% scaling).</summary>
    public static uint GetDpi(nint hwnd) => PInvoke.GetDpiForWindow((HWND)hwnd);

    /// <summary>
    /// True while Windows reports a fullscreen game, video or presentation somewhere. This is machine-wide: it says
    /// nothing about which monitor, so use it with <see cref="GetForegroundWindowInfo"/> (FullscreenPolicy).
    /// </summary>
    public static bool WindowsReportsFullscreenApp() =>
        PInvoke.SHQueryUserNotificationState(out var state).Succeeded
        && state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
            or QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
            or QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;

    /// <summary>The window in front and the monitor it is on, or null if there is none (or it is minimised).</summary>
    public static unsafe ForegroundWindow? GetForegroundWindowInfo()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull || PInvoke.IsIconic(hwnd) || !PInvoke.GetWindowRect(hwnd, out var rect))
            return null;

        // The desktop (Progman / WorkerW) and the taskbars cover whole monitors but are not apps.
        Span<char> buffer = stackalloc char[64];
        int length;
        fixed (char* p = buffer)
            length = PInvoke.GetClassName(hwnd, new PWSTR(p), buffer.Length);
        var className = new string(buffer[..length]);
        var isShell = className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";

        return new ForegroundWindow(new IntRect(rect.left, rect.top, rect.right, rect.bottom), MonitorBounds(hwnd), isShell);
    }

    /// <summary>The rectangle of the monitor the window is mostly on, in physical pixels.</summary>
    public static IntRect MonitorBounds(nint hwnd)
    {
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(PInvoke.MonitorFromWindow((HWND)hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST), ref info);
        return new IntRect(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom);
    }
}
