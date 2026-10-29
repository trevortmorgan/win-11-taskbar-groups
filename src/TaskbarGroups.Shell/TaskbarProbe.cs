using System.Runtime.InteropServices;
using TaskbarGroups.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;

namespace TaskbarGroups.Shell;

/// <summary>What one taskbar looks like right now. All rects are physical screen pixels.</summary>
/// <param name="Controls">Every Windows control on the taskbar (Start, Search, app buttons, tray, clock…). We must not cover these.</param>
/// <param name="AppButtonsRight">Right edge of Start and the app buttons, i.e. where Windows' own icons end.</param>
public sealed record TaskbarSnapshot(nint Handle, IntRect Bounds, IntRect Monitor, IReadOnlyList<IntRect> Controls, int AppButtonsRight, uint Dpi)
{
    /// <summary>False while an auto-hiding taskbar has slid off screen.</summary>
    public bool IsOnScreen => Monitor.Contains(Bounds);
    public bool IsHorizontal => Bounds.Width > Bounds.Height;
}

/// <summary>
/// Reads a taskbar's layout (the primary one, or a secondary one on another monitor) via UI Automation. Windows 11 draws the taskbar in XAML inside one window,
/// so UI Automation is the only supported way to see where its buttons are.
/// Create and call from a background (MTA) thread, never the UI thread.
/// </summary>
public sealed class TaskbarProbe
{
    private const string TaskListButtonClass = "Taskbar.TaskListButtonAutomationPeer";
    private const string StartButtonId = "StartButton";

    private readonly IUIAutomation _automation;
    private readonly IUIAutomationCacheRequest _cacheRequest;
    private readonly IUIAutomationCondition _allElements;

    public TaskbarProbe()
    {
        PInvoke.CoCreateInstance(typeof(CUIAutomation).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out _automation).ThrowOnFailure();
        _allElements = _automation.CreateTrueCondition();

        // Fetch only the properties we need, in one cross-process call, without live element references.
        _cacheRequest = _automation.CreateCacheRequest();
        _cacheRequest.AddProperty(UIA_PROPERTY_ID.UIA_ControlTypePropertyId);
        _cacheRequest.AddProperty(UIA_PROPERTY_ID.UIA_BoundingRectanglePropertyId);
        _cacheRequest.AddProperty(UIA_PROPERTY_ID.UIA_ClassNamePropertyId);
        _cacheRequest.AddProperty(UIA_PROPERTY_ID.UIA_AutomationIdPropertyId);
        _cacheRequest.put_AutomationElementMode(AutomationElementMode.AutomationElementMode_None);
    }

    /// <summary>
    /// The secondary taskbars, one per other monitor while *Show my taskbar on all displays* is on. Cheap; safe to
    /// call from any thread.
    /// </summary>
    public static IReadOnlyList<nint> FindSecondaryTaskbars()
    {
        var found = new List<nint>();
        var hwnd = HWND.Null;
        while (!(hwnd = PInvoke.FindWindowEx(HWND.Null, hwnd, "Shell_SecondaryTrayWnd", null)).IsNull)
            found.Add(hwnd);
        return found;
    }

    /// <summary>
    /// Reads the taskbar with this window handle, or the primary taskbar when <paramref name="taskbar"/> is null.
    /// Returns null if it doesn't exist (Explorer is restarting, or the monitor was unplugged).
    /// </summary>
    public TaskbarSnapshot? Read(nint? taskbar = null)
    {
        var hwnd = taskbar is { } given ? (HWND)given : PInvoke.FindWindow("Shell_TrayWnd", null);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd) || !PInvoke.GetWindowRect(hwnd, out var bounds))
            return null;

        var bar = ToIntRect(bounds);
        var controls = new List<IntRect>();
        var appButtonsRight = bar.Left;

        var elements = _automation.ElementFromHandle(hwnd).FindAllBuildCache(TreeScope.TreeScope_Descendants, _allElements, _cacheRequest);
        for (var i = 0; i < elements.get_Length(); i++)
        {
            var element = elements.GetElement(i);
            if (element.get_CachedControlType() != UIA_CONTROLTYPE_ID.UIA_ButtonControlTypeId)
                continue;

            var rect = ToIntRect(element.get_CachedBoundingRectangle());
            if (rect.IsEmpty)
                continue;
            controls.Add(rect);

            if (TakeString(element.get_CachedClassName()) == TaskListButtonClass || TakeString(element.get_CachedAutomationId()) == StartButtonId)
                appButtonsRight = Math.Max(appButtonsRight, rect.Right);
        }

        return new TaskbarSnapshot(hwnd, bar, MonitorBounds(hwnd), controls, appButtonsRight, PInvoke.GetDpiForWindow(hwnd));
    }

    private static IntRect MonitorBounds(HWND hwnd)
    {
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST), ref info);
        return ToIntRect(info.rcMonitor);
    }

    private static IntRect ToIntRect(RECT r) => new(r.left, r.top, r.right, r.bottom);

    /// <summary>Copies a BSTR returned by UI Automation into a string and frees it.</summary>
    private static unsafe string? TakeString(BSTR value)
    {
        if (value.Value is null)
            return null;
        var text = value.ToString();
        Marshal.FreeBSTR((nint)value.Value);
        return text;
    }
}
