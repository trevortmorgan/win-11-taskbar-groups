using TaskbarGroups.Switching;

namespace TaskbarGroups.Tests;

[TestClass]
public class WindowMatchingTests
{
    private sealed class FakeShortcuts(Dictionary<string, ShortcutInfo> files) : IShortcutReader
    {
        public ShortcutInfo? Read(string lnkPath) => files.GetValueOrDefault(lnkPath);
    }

    private const string Paint = @"C:\Program Files\WindowsApps\Microsoft.Paint_11.2605.81.0_x64__8wekyb3d8bbwe\PaintApp\mspaint.exe";
    private const string PaintAumid = "Microsoft.Paint_8wekyb3d8bbwe!App";

    [TestMethod]
    public void StoreApp_MatchesWindowWhoseProcessReportsTheAumid_WhateverTheExe() =>
        Assert.IsTrue(AppIdentity.Parse($@"shell:AppsFolder\{PaintAumid}").Matches(PaintAumid, Paint));

    [TestMethod]
    public void StoreApp_DoesNotMatchAnotherAppInTheSamePackage() =>
        Assert.IsFalse(AppIdentity.Parse($@"shell:AppsFolder\{PaintAumid}").Matches("Microsoft.Paint_8wekyb3d8bbwe!Other", Paint));

    [TestMethod]
    public void StoreExe_SurvivesAPackageUpdate()
    {
        var updated = Paint.Replace("11.2605.81.0", "11.2701.2.0");
        Assert.IsTrue(AppIdentity.Parse(Paint).Matches(null, updated));
    }

    [TestMethod]
    public void StoreExe_DoesNotMatchADifferentPackageOrExe()
    {
        var app = AppIdentity.Parse(Paint);
        Assert.IsFalse(app.Matches(null, Paint.Replace("Microsoft.Paint_", "Other.Paint_")));
        Assert.IsFalse(app.Matches(null, Paint.Replace("mspaint.exe", "other.exe")));
        Assert.IsFalse(app.Matches(null, Paint.Replace("_x64_", "_arm64_")));
    }

    [TestMethod]
    public void NonStorePath_NeedsAnExactMatch()
    {
        var app = AppIdentity.Parse(@"C:\Tools\1.0\app.exe");
        Assert.IsFalse(app.Matches(null, @"C:\Tools\2.0\app.exe"));
        Assert.IsFalse(app.Matches(null, @"C:\Other\app.exe"));
    }

    [TestMethod]
    public void HostProcess_NeverMatchesByPath()
    {
        const string host = @"C:\Windows\System32\ApplicationFrameHost.exe";
        Assert.IsFalse(AppIdentity.Parse(host).Matches(null, host));
        Assert.IsFalse(AppIdentity.Parse(@"C:\x\app.exe").Matches(null, host));
    }

    [TestMethod]
    public void Lnk_MatchesByItsTargetExe()
    {
        var reader = new FakeShortcuts(new() { [@"C:\Menu\App.lnk"] = new(@"C:\Tools\app.exe", null) });
        var app = AppIdentity.Parse(@"C:\Menu\App.lnk", reader);
        Assert.IsTrue(app.Matches(null, @"c:\tools\APP.exe"));
        Assert.IsFalse(app.Matches(null, @"C:\Tools\other.exe"));
    }

    [TestMethod]
    public void Lnk_MatchesByItsOwnAumid()
    {
        var reader = new FakeShortcuts(new() { [@"C:\Menu\Office.lnk"] = new(null, "Office.Excel") });
        var app = AppIdentity.Parse(@"C:\Menu\Office.lnk", reader);
        Assert.AreEqual(new AppIdentity("Office.Excel", null), app);
        Assert.IsTrue(app.Matches("office.excel", null));
    }

    [TestMethod]
    public void Lnk_ExpandsEnvironmentVariablesInTarget()
    {
        var reader = new FakeShortcuts(new() { [@"C:\a.lnk"] = new(@"%SystemRoot%\notepad.exe", null) });
        var expected = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\notepad.exe");
        Assert.AreEqual(expected, AppIdentity.Parse(@"C:\a.lnk", reader).ExePath);
    }

    [TestMethod]
    public void Lnk_ToAFolderUnreadableOrWithoutReader_MatchesNothing()
    {
        var reader = new FakeShortcuts(new() { [@"C:\folder.lnk"] = new(@"C:\Docs", null) });
        Assert.AreEqual(new AppIdentity(null, null), AppIdentity.Parse(@"C:\folder.lnk", reader));
        Assert.AreEqual(new AppIdentity(null, null), AppIdentity.Parse(@"C:\missing.lnk", reader));
        Assert.AreEqual(new AppIdentity(null, null), AppIdentity.Parse(@"C:\folder.lnk"));
    }

    [TestMethod]
    public void Indicator_LightsForTheRightWindowOnly()
    {
        var terminal = AppIdentity.Parse(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App");
        List<WindowInfo> windows =
        [
            new(1, PaintAumid, Paint),
            new(2, "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24.0.0_x64__8wekyb3d8bbwe\WindowsTerminal.exe"),
            new(3, null, @"C:\Windows\System32\ApplicationFrameHost.exe"),
        ];
        Assert.AreEqual(RunningState.Running, terminal.StateIn(windows, foreground: 1));
        Assert.AreEqual(RunningState.Focused, terminal.StateIn(windows, foreground: 2));
        Assert.AreEqual(RunningState.NotRunning, terminal.StateIn([windows[0], windows[2]], foreground: 1));
    }
}
