using TaskbarGroups.Layout;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class TaskbarSyncTests
{
    [TestMethod]
    public void Diff_NewMonitor_IsAdded()
    {
        var (added, removed) = TaskbarSync.Diff(current: [10, 20], existing: [10]);

        CollectionAssert.AreEqual(new nint[] { 20 }, added.ToArray());
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void Diff_UnpluggedMonitor_IsRemoved()
    {
        var (added, removed) = TaskbarSync.Diff(current: [], existing: [10, 20]);

        Assert.AreEqual(0, added.Count);
        CollectionAssert.AreEquivalent(new nint[] { 10, 20 }, removed.ToArray());
    }

    [TestMethod]
    public void Diff_RecreatedTaskbar_IsOneRemovedAndOneAdded()
    {
        var (added, removed) = TaskbarSync.Diff(current: [30], existing: [10]);

        CollectionAssert.AreEqual(new nint[] { 30 }, added.ToArray());
        CollectionAssert.AreEqual(new nint[] { 10 }, removed.ToArray());
    }

    [TestMethod]
    public void Diff_Unchanged_DoesNothing()
    {
        var (added, removed) = TaskbarSync.Diff(current: [10, 20, 20], existing: [20, 10]);

        Assert.AreEqual(0, added.Count);
        Assert.AreEqual(0, removed.Count);
    }
}
