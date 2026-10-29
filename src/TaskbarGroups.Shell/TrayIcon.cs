using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TaskbarGroups.Shell;

/// <summary>One line in the tray icon's right-click menu.</summary>
/// <param name="Text">Menu text; null makes this a separator.</param>
/// <param name="Action">Runs when the item is chosen.</param>
/// <param name="Checked">Shows a tick beside the item.</param>
public sealed record TrayMenuItem(string? Text, Action? Action = null, bool Checked = false)
{
    public static TrayMenuItem Separator { get; } = new(null);
}

/// <summary>
/// A notification-area icon with a native right-click menu. The menu is built from <paramref name="buildMenu"/> each
/// time it opens, so check marks always reflect the current state. The icon is re-added when Explorer restarts.
/// Create on the UI thread: Windows delivers the icon's messages to a hidden window on that thread.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const string WindowClass = "TaskbarGroups.TrayWindow";
    private const uint CallbackMessage = PInvoke.WM_USER + 1;
    private const uint IconId = 1;
    private const uint NinSelect = PInvoke.WM_USER;
    private const uint NinKeySelect = PInvoke.WM_USER + 1;

    // The window procedure is static, so route messages to the (single) live instance.
    private static TrayIcon? s_instance;

    private readonly Func<IReadOnlyList<TrayMenuItem>> _buildMenu;
    private readonly string _tooltip;
    private readonly string? _iconPath;
    private readonly uint _taskbarCreated;
    private readonly HWND _hwnd;
    private HICON _icon;
    private bool _added;

    /// <summary>Raised on a left click (or Enter/Space on the focused icon).</summary>
    public event Action? Selected;

    public unsafe TrayIcon(string tooltip, string? iconPath, Func<IReadOnlyList<TrayMenuItem>> buildMenu)
    {
        if (s_instance is not null)
            throw new InvalidOperationException("Only one TrayIcon may exist.");
        s_instance = this;
        _tooltip = tooltip;
        _iconPath = iconPath;
        _buildMenu = buildMenu;
        _taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");

        // A hidden top-level window (not message-only, which wouldn't get the TaskbarCreated broadcast).
        fixed (char* className = WindowClass)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                lpszClassName = className,
            };
            PInvoke.RegisterClassEx(in wc);
            _hwnd = PInvoke.CreateWindowEx(0, WindowClass, WindowClass, 0, 0, 0, 0, 0, HWND.Null, null, null, null);
        }
        if (_hwnd.IsNull)
            throw new InvalidOperationException("Couldn't create the tray window.");

        _icon = LoadIcon();
        AddIcon();
    }

    private unsafe HICON LoadIcon()
    {
        if (_iconPath is not null)
        {
            HICON small = default;
            fixed (char* path = _iconPath)
                if (PInvoke.ExtractIconEx(path, 0, null, &small, 1) > 0 && !small.IsNull)
                    return small;
        }
        return PInvoke.LoadIcon(HINSTANCE.Null, PInvoke.IDI_APPLICATION);
    }

    private unsafe NOTIFYICONDATAW NewData()
    {
        var data = new NOTIFYICONDATAW { cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = _hwnd, uID = IconId };
        return data;
    }

    private unsafe void AddIcon()
    {
        var data = NewData();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        data.szTip = _tooltip;
        _added = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data);

        // Version 4 reports keyboard use and the menu position properly.
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in data);
    }

    private unsafe void ShowMenu(int x, int y)
    {
        var items = _buildMenu();
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var flags = item.Text is null ? MENU_ITEM_FLAGS.MF_SEPARATOR
                    : item.Checked ? MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_CHECKED
                    : MENU_ITEM_FLAGS.MF_STRING;
                fixed (char* text = item.Text)
                    PInvoke.AppendMenu(menu, flags, (nuint)(i + 1), text);
            }

            // Without this the menu doesn't close when you click elsewhere.
            PInvoke.SetForegroundWindow(_hwnd);
            var chosen = (int)PInvoke.TrackPopupMenuEx(menu,
                (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN),
                x, y, _hwnd, null).Value;
            PInvoke.PostMessage(_hwnd, PInvoke.WM_NULL, 0, 0);

            if (chosen > 0)
                items[chosen - 1].Action?.Invoke();
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            var tray = s_instance;
            if (tray is not null)
            {
                if (msg == CallbackMessage)
                {
                    switch ((uint)(lParam.Value & 0xFFFF))
                    {
                        case PInvoke.WM_CONTEXTMENU:
                            tray.ShowMenu((short)(wParam.Value & 0xFFFF), (short)((wParam.Value >> 16) & 0xFFFF));
                            break;
                        case NinSelect:
                        case NinKeySelect:
                            tray.Selected?.Invoke();
                            break;
                    }
                    return (LRESULT)0;
                }
                if (msg == tray._taskbarCreated)
                {
                    tray.AddIcon(); // Explorer restarted and lost our icon
                    return (LRESULT)0;
                }
            }
        }
        catch
        {
            // An exception must never cross into native code.
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = NewData();
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
            _added = false;
        }
        PInvoke.DestroyWindow(_hwnd);
        if (!_icon.IsNull)
            PInvoke.DestroyIcon(_icon);
        s_instance = null;
    }
}
