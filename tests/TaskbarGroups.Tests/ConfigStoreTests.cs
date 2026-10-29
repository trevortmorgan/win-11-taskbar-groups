using TaskbarGroups.Config;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class ConfigStoreTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Setup() => _dir = Directory.CreateTempSubdirectory("tbg-tests-").FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_dir, recursive: true);

    [TestMethod]
    public void Load_WhenNoFile_ReturnsSample()
    {
        var store = new ConfigStore(Path.Combine(_dir, "groups.json"));

        Assert.AreSame(GroupsConfig.Sample, store.Load());
    }

    [TestMethod]
    public void LoadOrCreate_WhenNoFile_WritesSampleToDisk()
    {
        var path = Path.Combine(_dir, "groups.json");
        var store = new ConfigStore(path);

        var loaded = store.LoadOrCreate();

        Assert.IsTrue(File.Exists(path));
        Assert.HasCount(GroupsConfig.Sample.Groups.Count, loaded.Groups);
        Assert.AreEqual(GroupsConfig.Sample.Groups[0].Apps[0], loaded.Groups[0].Apps[0]);
    }

    [TestMethod]
    public void ResolvePath_ExpandsEnvironmentVariables()
    {
        var app = new AppShortcut("Explorer", @"%WINDIR%\explorer.exe");

        Assert.AreEqual(Path.Combine(Environment.GetEnvironmentVariable("WINDIR")!, "explorer.exe"), app.ResolvePath());
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsGroups()
    {
        var store = new ConfigStore(Path.Combine(_dir, "nested", "groups.json"));
        var config = new GroupsConfig(
        [
            new Group("Media", "#22C55E", [new AppShortcut("Player", @"C:\Apps\player.exe", "--minimised")]),
        ]);

        store.Save(config);
        var loaded = store.Load();

        Assert.HasCount(1, loaded.Groups);
        Assert.AreEqual("Media", loaded.Groups[0].Name);
        Assert.AreEqual("#22C55E", loaded.Groups[0].Color);
        Assert.AreEqual(config.Groups[0].Apps[0], loaded.Groups[0].Apps[0]);
        Assert.IsNull(loaded.Groups[0].Icon);
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsIcon_AndLeavesItOutWhenUnset()
    {
        var path = Path.Combine(_dir, "groups.json");
        var store = new ConfigStore(path);
        var config = new GroupsConfig(
        [
            new Group("Work", "#3B82F6", [new AppShortcut("Notepad", "notepad.exe")], Icon: "briefcase"),
            new Group("Misc", "#22C55E", [new AppShortcut("Calc", "calc.exe")]),
        ]);

        store.Save(config);
        var json = File.ReadAllText(path);
        var loaded = store.Load();

        Assert.AreEqual("briefcase", loaded.Groups[0].Icon);
        Assert.IsNull(loaded.Groups[1].Icon);
        Assert.AreEqual(1, json.Split("\"icon\"").Length - 1, json);
        Assert.IsLessThan(json.IndexOf("\"apps\""), json.IndexOf("\"icon\""), "icon should be written before apps");
    }

    [TestMethod]
    public void Load_ReadsIconFromHandWrittenJson()
    {
        var path = Path.Combine(_dir, "groups.json");
        File.WriteAllText(path, """
            { "groups": [ { "name": "Games", "color": "#A855F7", "icon": "games", "apps": [] } ] }
            """);

        Assert.AreEqual("games", new ConfigStore(path).Load().Groups[0].Icon);
    }
}
