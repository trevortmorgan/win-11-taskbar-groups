using TaskbarGroups.Layout;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class TaskbarGapFinderTests
{
    // A 3840px-wide, 72px-high taskbar at the bottom of a 2160px-high screen (the dev machine's real layout).
    private static readonly IntRect Taskbar = new(0, 2088, 3840, 2160);

    private static IntRect Button(int left, int width) => IntRect.FromSize(left, 2088, width, 72);

    [TestMethod]
    public void FindGaps_CentredTaskbar_ReturnsSpaceEitherSideOfApps()
    {
        var occupied = new[] { Button(1424, 68), Button(1492, 66), Button(1558, 66), Button(3285, 555) };

        var gaps = TaskbarGapFinder.FindGaps(Taskbar, occupied, margin: 0);

        CollectionAssert.AreEqual(new[] { new IntRect(0, 2088, 1424, 2160), new IntRect(1624, 2088, 3285, 2160) }, gaps.ToArray());
    }

    [TestMethod]
    public void FindGaps_KeepsMarginClearOfControlsAndEdges()
    {
        var gaps = TaskbarGapFinder.FindGaps(Taskbar, [Button(1000, 100)], margin: 12);

        CollectionAssert.AreEqual(new[] { new IntRect(12, 2088, 988, 2160), new IntRect(1112, 2088, 3828, 2160) }, gaps.ToArray());
    }

    [TestMethod]
    public void FindGaps_OverlappingAndUnsortedControls_AreMerged()
    {
        var gaps = TaskbarGapFinder.FindGaps(Taskbar, [Button(200, 100), Button(0, 250), Button(280, 20)], margin: 0);

        CollectionAssert.AreEqual(new[] { new IntRect(300, 2088, 3840, 2160) }, gaps.ToArray());
    }

    [TestMethod]
    public void PickGap_PrefersGapAfterAppButtons_EvenIfNarrower()
    {
        IntRect[] gaps = [new(0, 2088, 1424, 2160), new(2416, 2088, 3285, 2160)];

        Assert.AreEqual(gaps[1], TaskbarGapFinder.PickGap(gaps, appButtonsRight: 2416, minWidth: 100));
    }

    [TestMethod]
    public void PickGap_FallsBackToWidest_WhenGapAfterAppsTooSmall()
    {
        IntRect[] gaps = [new(0, 2088, 1424, 2160), new(2416, 2088, 2450, 2160)];

        Assert.AreEqual(gaps[0], TaskbarGapFinder.PickGap(gaps, appButtonsRight: 2416, minWidth: 100));
    }

    [TestMethod]
    public void PickGap_ReturnsNull_WhenNothingFits()
    {
        Assert.IsNull(TaskbarGapFinder.PickGap([new(0, 0, 50, 72)], appButtonsRight: 0, minWidth: 100));
    }
}
