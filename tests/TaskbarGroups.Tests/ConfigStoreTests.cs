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
    }
}
