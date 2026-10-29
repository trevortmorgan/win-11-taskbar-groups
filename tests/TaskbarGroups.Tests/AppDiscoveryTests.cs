using TaskbarGroups.Config;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class AppDiscoveryTests
{
    [TestMethod]
    public void FromPath_NamesAppAfterFileWithoutExtension()
    {
        var app = AppDiscovery.FromPath(@"C:\Tools\Visual Studio Code.lnk");

        Assert.AreEqual("Visual Studio Code", app.Name);
        Assert.AreEqual(@"C:\Tools\Visual Studio Code.lnk", app.Path);
    }

    [TestMethod]
    public void FromPath_Folder_UsesFolderName()
    {
        Assert.AreEqual("Reports", AppDiscovery.FromPath(@"D:\Reports\").Name);
    }

    [TestMethod]
    public void ParseStartApps_ReadsArrayAndSortsByName()
    {
        var apps = AppDiscovery.ParseStartApps("""
            [{"Name":"Paint","AppID":"Microsoft.Paint_8wekyb3d8bbwe!App"},
             {"Name":"Calculator","AppID":"Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"},
             {"Name":"","AppID":"skipped"}]
            """);

        Assert.HasCount(2, apps);
        Assert.AreEqual("Calculator", apps[0].Name);
        Assert.AreEqual(@"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App", apps[1].ToShortcut().Path);
    }

    [TestMethod]
    public void ParseStartApps_SingleObject_IsOneApp()
    {
        var apps = AppDiscovery.ParseStartApps("""{"Name":"Edge","AppID":"MSEdge"}""");

        Assert.HasCount(1, apps);
    }

    [TestMethod]
    public void ParseStartApps_Empty_IsNoApps()
    {
        Assert.IsEmpty(AppDiscovery.ParseStartApps(""));
    }
}
