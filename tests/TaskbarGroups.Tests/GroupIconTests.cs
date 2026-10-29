using System.Xml.Linq;
using TaskbarGroups.Config;
using TaskbarGroups.Icons;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class GroupIconTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Setup() => _dir = Directory.CreateTempSubdirectory("tbg-tests-").FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_dir, recursive: true);

    [TestMethod]
    public void BundledNames_IncludeCommonIcons()
    {
        CollectionAssert.IsSubsetOf(new[] { "briefcase", "games", "globe", "music-note-2", "code", "home", "heart" },
            GroupIcon.BundledNames.ToList());
    }

    [TestMethod]
    public void SampleConfig_IconsAllResolve()
    {
        foreach (var group in GroupsConfig.Sample.Groups)
            Assert.IsTrue(GroupIcon.TryResolve(group.Icon, _dir, out _, out var error), error);
    }

    [TestMethod]
    [DataRow("briefcase")]
    [DataRow("Briefcase")]
    [DataRow(" music note 2 ")]
    [DataRow("music_note_2")]
    public void TryResolve_FindsBundledIconsByLooseName(string name)
    {
        Assert.IsTrue(GroupIcon.TryResolve(name, _dir, out var icon, out var error), error);
        Assert.IsNotNull(icon);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void TryResolve_NoIcon_IsNotAnError(string? name)
    {
        Assert.IsFalse(GroupIcon.TryResolve(name, _dir, out var icon, out var error));
        Assert.IsNull(icon);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryResolve_UnknownName_ExplainsWhy()
    {
        Assert.IsFalse(GroupIcon.TryResolve("no-such-icon", _dir, out _, out var error));
        Assert.Contains("no-such-icon", error!);
    }

    [TestMethod]
    [DataRow(new[] { 16, 20, 24 }, 24, 24)] // 150%: drawn for exactly this size
    [DataRow(new[] { 16, 20, 24 }, 20, 20)] // 125%
    [DataRow(new[] { 16, 20, 24 }, 32, 16)] // 200%: 16 doubles cleanly
    [DataRow(new[] { 16, 20, 24 }, 28, 24)] // 175%: nothing fits exactly, so the biggest
    [DataRow(new[] { 16, 20, 24 }, 18, 20)] // next size up rather than down
    [DataRow(new[] { 20 }, 24, 20)]
    public void PickDesignSize_PrefersExactThenWholeMultipleThenLarger(int[] sizes, int pixels, int expected)
    {
        Assert.AreEqual(expected, GroupIcon.PickDesignSize(sizes, pixels));
    }

    [TestMethod]
    public void ToSvg_BundledIcon_IsSizedAndColoured()
    {
        GroupIcon.TryResolve("briefcase", _dir, out var icon, out _);

        var svg = XElement.Parse(icon!.ToSvg(24, "#3B82F6"));

        Assert.AreEqual("24", svg.Attribute("width")!.Value);
        Assert.AreEqual("24", svg.Attribute("height")!.Value);
        Assert.AreEqual("0 0 24 24", svg.Attribute("viewBox")!.Value); // the 24 px drawing, not a scaled 16
        Assert.IsTrue(svg.Descendants().Attributes("fill").All(a => a.Value == "#3B82F6"));
    }

    [TestMethod]
    public void TryResolve_SvgFile_RelativeToConfigFolder_AndTakesGroupColour()
    {
        // Typical of a file saved from icones.js.org: currentColor, sized in em.
        File.WriteAllText(Path.Combine(_dir, "rocket.svg"),
            """<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 24"><path fill="none" stroke="currentColor" stroke-width="2" d="M4 4h16v16H4z"/></svg>""");

        Assert.IsTrue(GroupIcon.TryResolve("rocket.svg", _dir, out var icon, out var error), error);
        var svg = XElement.Parse(icon.ToSvg(24, "#22C55E"));

        Assert.AreEqual("24", svg.Attribute("width")!.Value);
        Assert.AreEqual("0 0 24 24", svg.Attribute("viewBox")!.Value);
        var path = svg.Elements().Single();
        Assert.AreEqual("#22C55E", path.Attribute("stroke")!.Value);
        Assert.AreEqual("none", path.Attribute("fill")!.Value);
    }

    [TestMethod]
    public void ToSvg_MultiColourFile_KeepsItsColours()
    {
        File.WriteAllText(Path.Combine(_dir, "flag.svg"),
            """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><path fill="#FF0000" d="M0 0h16v8H0z"/><path fill="#FFFFFF" d="M0 8h16v8H0z"/></svg>""");

        GroupIcon.TryResolve("flag.svg", _dir, out var icon, out _);
        var fills = XElement.Parse(icon!.ToSvg(16, "#22C55E")).Elements().Select(e => e.Attribute("fill")!.Value);

        CollectionAssert.AreEqual(new[] { "#FF0000", "#FFFFFF" }, fills.ToList());
    }

    [TestMethod]
    public void TryResolve_MissingOrInvalidFile_ExplainsWhy()
    {
        File.WriteAllText(Path.Combine(_dir, "broken.svg"), "<html>not an svg</html>");

        Assert.IsFalse(GroupIcon.TryResolve("missing.svg", _dir, out _, out var missing));
        Assert.IsFalse(GroupIcon.TryResolve("broken.svg", _dir, out _, out var broken));

        Assert.Contains("not found", missing!);
        Assert.Contains("not an SVG", broken!);
    }

    [TestMethod]
    public void TryResolve_FileWithDtd_IsRefused()
    {
        File.WriteAllText(Path.Combine(_dir, "dtd.svg"),
            """<!DOCTYPE svg [<!ENTITY x "x">]><svg xmlns="http://www.w3.org/2000/svg">&x;</svg>""");

        Assert.IsFalse(GroupIcon.TryResolve("dtd.svg", _dir, out _, out var error));
        Assert.IsNotNull(error);
    }
}
