using TaskbarGroups.Switching;

namespace TaskbarGroups.Tests;

[TestClass]
public class ClickActionTests
{
    [TestMethod]
    public void NotRunning_Launches() =>
        Assert.AreEqual(ClickKind.Launch, ClickAction.Decide([], foreground: 5).Kind);

    [TestMethod]
    public void RunningInBackground_ActivatesMostRecent()
    {
        var action = ClickAction.Decide([10, 20], foreground: 5);
        Assert.AreEqual(new ClickAction(ClickKind.Activate, 10), action);
    }

    [TestMethod]
    public void InFront_Minimizes()
    {
        var action = ClickAction.Decide([10, 20], foreground: 20);
        Assert.AreEqual(new ClickAction(ClickKind.Minimize, 20), action);
    }

    [TestMethod]
    public void Shift_AlwaysLaunches() =>
        Assert.AreEqual(ClickKind.Launch, ClickAction.Decide([10], foreground: 10, newWindow: true).Kind);

    [TestMethod]
    public void Identity_StoreAppMatchesAumid_ExeMatchesPath()
    {
        var store = AppIdentity.Parse(@"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App");
        Assert.IsTrue(store.Matches("microsoft.paint_8wekyb3d8bbwe!app", null));
        Assert.IsFalse(store.Matches(null, @"C:\x.exe"));

        var exe = AppIdentity.Parse(@"C:\Tools\App.exe");
        Assert.IsTrue(exe.Matches("whatever", @"c:\tools\app.EXE"));
        Assert.IsFalse(AppIdentity.Parse(@"C:\Docs").Matches(null, null));
    }
}

[TestClass]
public class RunningStateTests
{
    private static readonly AppIdentity Notepad = new(null, @"C:\Windows\notepad.exe");

    private static WindowInfo Win(nint handle, string exe) => new(handle, null, exe);

    [TestMethod]
    public void NoMatchingWindow_NotRunning() =>
        Assert.AreEqual(RunningState.NotRunning, Notepad.StateIn([Win(1, @"C:\other.exe")], foreground: 1));

    [TestMethod]
    public void MatchingWindowInBackground_Running() =>
        Assert.AreEqual(RunningState.Running, Notepad.StateIn([Win(1, @"c:\windows\NOTEPAD.exe")], foreground: 9));

    [TestMethod]
    public void MatchingWindowInFront_Focused() =>
        Assert.AreEqual(RunningState.Focused, Notepad.StateIn([Win(2, @"C:\Windows\notepad.exe"), Win(1, @"C:\Windows\notepad.exe")], foreground: 1));

    [TestMethod]
    public void StoreApp_MatchesByAumid() =>
        Assert.AreEqual(RunningState.Running,
            new AppIdentity("Pkg!App", null).StateIn([new WindowInfo(1, "pkg!app", null)], foreground: 9));

    [TestMethod]
    public void UnknownIdentity_NeverRunning() =>
        Assert.AreEqual(RunningState.NotRunning, new AppIdentity(null, null).StateIn([new WindowInfo(1, null, null)], foreground: 1));
}
