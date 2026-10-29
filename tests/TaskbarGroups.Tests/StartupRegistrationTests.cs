using Microsoft.Win32;
using TaskbarGroups.Startup;

namespace TaskbarGroups.Tests;

[TestClass]
public sealed class StartupRegistrationTests
{
    private readonly string _key = @"Software\TaskbarGroupsTests\" + Guid.NewGuid().ToString("N");

    // Tests run in parallel, so only delete this test's own key; sharing the parent races.
    [TestCleanup]
    public void Cleanup()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
        try { Registry.CurrentUser.DeleteSubKey(@"Software\TaskbarGroupsTests", throwOnMissingSubKey: false); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { } // still holds another test's key
    }

    [TestMethod]
    public void Enable_then_disable_round_trips_through_the_registry()
    {
        var startup = new StartupRegistration(@"""C:\Apps\TaskbarGroups.exe""", "Test", _key);
        Assert.IsFalse(startup.IsEnabled);

        startup.SetEnabled(true);
        Assert.IsTrue(startup.IsEnabled);
        Assert.IsTrue(new StartupRegistration(@"""C:\Apps\TaskbarGroups.exe""", "Test", _key).IsEnabled, "a fresh instance reads the same state");

        startup.SetEnabled(false);
        Assert.IsFalse(startup.IsEnabled);
        startup.SetEnabled(false); // already off: no error
    }

    [TestMethod]
    public void A_value_pointing_at_another_exe_is_not_enabled()
    {
        new StartupRegistration(@"""C:\Old\TaskbarGroups.exe""", "Test", _key).SetEnabled(true);
        Assert.IsFalse(new StartupRegistration(@"""C:\New\TaskbarGroups.exe""", "Test", _key).IsEnabled);
    }
}
