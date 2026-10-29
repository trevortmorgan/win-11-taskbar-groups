using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell.PropertiesSystem;
using Windows.Win32.UI.WindowsAndMessaging;
using TaskbarGroups.Switching;

namespace TaskbarGroups.Shell;

/// <summary>
/// Finds the windows that have a taskbar button and brings one to the front or minimises it. Only reads, shows,
/// restores and minimises other apps' windows: never changes their styles or owner.
/// </summary>
public static class WindowSwitcher
{
    // System.AppUserModel.ID: {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5
    private static readonly PROPERTYKEY AppUserModelId = new()
    {
        fmtid = new Guid(0x9F4C2855, 0x9F79, 0x4B39, 0xA8, 0xD0, 0xE1, 0xD4, 0x2D, 0xE1, 0xD5, 0xF3),
        pid = 5,
    };

    /// <summary>Windows of the app, most recently used first (Windows keeps top-level windows in that order).</summary>
    public static List<nint> FindWindows(AppIdentity app)
    {
        var found = new List<nint>();
        if (app is { Aumid: null, ExePath: null })
            return found;

        for (var h = PInvoke.GetTopWindow(HWND.Null); h != HWND.Null; h = PInvoke.GetWindow(h, GET_WINDOW_CMD.GW_HWNDNEXT))
        {
            if (!HasTaskbarButton(h))
                continue;
            if (app.Matches(GetAumid(h), app.ExePath is null ? null : GetExePath(h)))
                found.Add(h);
        }
        return found;
    }

    /// <summary>True while Shift is held: the Windows convention for "open a new window".</summary>
    public static bool IsShiftDown() => (PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0;

    public static void Minimize(nint hwnd) => PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_MINIMIZE);

    /// <summary>Restores the window if minimised, then brings it to the front with keyboard focus.</summary>
    public static unsafe void Activate(nint hwnd)
    {
        var h = (HWND)hwnd;
        if (PInvoke.IsIconic(h))
            PInvoke.ShowWindow(h, SHOW_WINDOW_CMD.SW_RESTORE);

        // Windows only lets the process that owns the foreground window (or received the last input) take it, and
        // otherwise just flashes the taskbar button. Our strip is no-activate, so the app we're replacing still owns
        // the foreground: sharing its input queue for a moment makes the switch allowed.
        var foreground = PInvoke.GetForegroundWindow();
        var foregroundThread = foreground == HWND.Null ? 0 : PInvoke.GetWindowThreadProcessId(foreground, null);
        var ourThread = PInvoke.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ourThread
            && PInvoke.AttachThreadInput(ourThread, foregroundThread, true);
        try
        {
            PInvoke.BringWindowToTop(h);
            PInvoke.SetForegroundWindow(h);
        }
        finally
        {
            if (attached)
                PInvoke.AttachThreadInput(ourThread, foregroundThread, false);
        }
    }

    /// <summary>Would get a button on the taskbar: visible, not a tool window or owned popup, and not on another desktop.</summary>
    private static unsafe bool HasTaskbarButton(HWND h)
    {
        if (!PInvoke.IsWindowVisible(h))
            return false;

        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if ((exStyle & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0)
            return false;
        if (PInvoke.GetWindow(h, GET_WINDOW_CMD.GW_OWNER) != HWND.Null && (exStyle & WINDOW_EX_STYLE.WS_EX_APPWINDOW) == 0)
            return false;

        uint cloaked = 0;
        PInvoke.DwmGetWindowAttribute(h, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return cloaked == 0;
    }

    private static unsafe string? GetAumid(HWND h)
    {
        if (PInvoke.SHGetPropertyStoreForWindow<IPropertyStore>(h, out var store).Failed)
            return null;
        try
        {
            store.GetValue(AppUserModelId, out var value);
            try
            {
                if (PInvoke.PropVariantToStringAlloc(value, out var text).Failed)
                    return null;
                var result = text.ToString();
                PInvoke.CoTaskMemFree(text);
                return string.IsNullOrEmpty(result) ? null : result;
            }
            finally
            {
                PInvoke.PropVariantClear(ref value);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static unsafe string? GetExePath(HWND h)
    {
        uint pid;
        if (PInvoke.GetWindowThreadProcessId(h, &pid) == 0)
            return null;
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsInvalid)
            return null;

        Span<char> buffer = stackalloc char[1024];
        uint length = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref length)
            ? new string(buffer[..(int)length])
            : null;
    }
}
