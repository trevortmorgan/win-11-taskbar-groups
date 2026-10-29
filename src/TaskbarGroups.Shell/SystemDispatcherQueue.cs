using Windows.Win32;
using Windows.Win32.System.WinRT;

namespace TaskbarGroups.Shell;

/// <summary>
/// WinUI threads run a Microsoft.UI DispatcherQueue, but the system compositor (Windows.UI.Composition, which window
/// backdrops use) needs the older Windows.System one on the same thread. This adds it.
/// </summary>
public static class SystemDispatcherQueue
{
    // Kept alive for the life of the process; the queue stops if the controller is collected.
    private static global::Windows.System.DispatcherQueueController? s_controller;

    public static unsafe void EnsureForCurrentThread()
    {
        if (global::Windows.System.DispatcherQueue.GetForCurrentThread() is not null)
            return;

        var options = new DispatcherQueueOptions
        {
            dwSize = (uint)sizeof(DispatcherQueueOptions),
            threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
            apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_NONE,
        };
        PInvoke.CreateDispatcherQueueController(options, out s_controller).ThrowOnFailure();
    }
}
