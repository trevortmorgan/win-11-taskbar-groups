using TaskbarGroups.Layout;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class FullscreenPolicyTests
{
    private static readonly IntRect Left = new(0, 0, 3840, 2160);
    private static readonly IntRect Right = new(3840, 0, 5760, 3840); // portrait, to the right

    [TestMethod]
    public void WindowCoveringThisMonitor_IsFullscreen()
    {
        var fg = new ForegroundWindow(Left, Left, IsShell: false);

        Assert.IsTrue(FullscreenPolicy.IsFullscreenOn(Left, fg, windowsReportsFullscreen: false));
    }

    [TestMethod]
    public void FullscreenOnTheOtherMonitor_DoesNotHideThisOne()
    {
        var fg = new ForegroundWindow(Right, Right, IsShell: false);

        Assert.IsFalse(FullscreenPolicy.IsFullscreenOn(Left, fg, windowsReportsFullscreen: true));
        Assert.IsTrue(FullscreenPolicy.IsFullscreenOn(Right, fg, windowsReportsFullscreen: true));
    }

    [TestMethod]
    public void MaximisedWindow_LeavesTheTaskbarAlone()
    {
        var workArea = new IntRect(0, 0, 3840, 2088); // monitor minus the taskbar
        var fg = new ForegroundWindow(workArea, Left, IsShell: false);

        Assert.IsFalse(FullscreenPolicy.IsFullscreenOn(Left, fg, windowsReportsFullscreen: false));
    }

    [TestMethod]
    public void WindowsReportingFullscreen_CountsOnTheForegroundsMonitor()
    {
        // e.g. an exclusive-mode game whose window rect is a little off the monitor's
        var fg = new ForegroundWindow(new IntRect(0, 0, 3840, 2159), Left, IsShell: false);

        Assert.IsTrue(FullscreenPolicy.IsFullscreenOn(Left, fg, windowsReportsFullscreen: true));
        Assert.IsFalse(FullscreenPolicy.IsFullscreenOn(Left, fg, windowsReportsFullscreen: false));
    }

    [TestMethod]
    public void DesktopOrTaskbarInFront_IsNeverFullscreen()
    {
        var fg = new ForegroundWindow(Left, Left, IsShell: true);

        Assert.IsFalse(FullscreenPolicy.IsFullscreenOn(Left, fg, windowsReportsFullscreen: true));
    }

    [TestMethod]
    public void NoForegroundWindow_IsNotFullscreen()
    {
        Assert.IsFalse(FullscreenPolicy.IsFullscreenOn(Left, null, windowsReportsFullscreen: true));
    }
}
