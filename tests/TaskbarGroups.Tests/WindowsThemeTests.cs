using TaskbarGroups.Theming;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class WindowsThemeTests
{
    [TestMethod]
    public void One_is_light() => Assert.IsTrue(WindowsTheme.IsLight(1));

    [TestMethod]
    public void Zero_is_dark() => Assert.IsFalse(WindowsTheme.IsLight(0));

    [TestMethod]
    public void Missing_or_unexpected_values_are_dark()
    {
        Assert.IsFalse(WindowsTheme.IsLight(null));
        Assert.IsFalse(WindowsTheme.IsLight("1"));
        Assert.IsFalse(WindowsTheme.IsLight(2));
    }
}
