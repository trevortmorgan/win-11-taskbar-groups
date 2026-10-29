using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace TaskbarGroups.Shell;

/// <summary>
/// Raises <see cref="ForegroundChanged"/> whenever any window becomes the foreground window (including the taskbar
/// when it's clicked). Create on the UI thread: callbacks arrive via that thread's message loop.
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    // The native callback is static, so route it to the (single) live instance.
    private static ForegroundWatcher? s_instance;
    private readonly HWINEVENTHOOK _hook;

    public event Action<nint>? ForegroundChanged;

    public unsafe ForegroundWatcher()
    {
        if (s_instance is not null)
            throw new InvalidOperationException("Only one ForegroundWatcher may exist.");
        s_instance = this;
        _hook = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, HMODULE.Null,
            &OnWinEvent, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnWinEvent(HWINEVENTHOOK hook, uint eventType, HWND hwnd, int idObject, int idChild, uint thread, uint time) =>
        s_instance?.ForegroundChanged?.Invoke(hwnd);

    public void Dispose()
    {
        PInvoke.UnhookWinEvent(_hook);
        s_instance = null;
    }
}
