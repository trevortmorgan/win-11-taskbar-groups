using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;
using TaskbarGroups.Switching;

namespace TaskbarGroups.Shell;

/// <summary>Reads a .lnk's target program and its own AppUserModelID (set on Start menu shortcuts of Store apps).</summary>
public sealed class ShortcutReader : IShortcutReader
{
    // System.AppUserModel.ID: {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5
    private static readonly PROPERTYKEY AppUserModelId = new()
    {
        fmtid = new Guid(0x9F4C2855, 0x9F79, 0x4B39, 0xA8, 0xD0, 0xE1, 0xD4, 0x2D, 0xE1, 0xD5, 0xF3),
        pid = 5,
    };

    public ShortcutInfo? Read(string lnkPath)
    {
        try
        {
            return new ShortcutInfo(ReadTarget(lnkPath), ReadAumid(lnkPath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static unsafe string? ReadTarget(string lnkPath)
    {
        PInvoke.CoCreateInstance(typeof(ShellLink).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out IShellLinkW link).ThrowOnFailure();
        var file = (IPersistFile)link;
        file.Load(lnkPath, 0 /* STGM_READ */);

        Span<char> buffer = stackalloc char[1024];
        fixed (char* p = buffer)
        {
            link.GetPath(p, buffer.Length, null, 0);
            var target = new string(p);
            return target.Length == 0 ? null : target;
        }
    }

    private static unsafe string? ReadAumid(string lnkPath)
    {
        if (PInvoke.SHGetPropertyStoreFromParsingName(lnkPath, null, GETPROPERTYSTOREFLAGS.GPS_DEFAULT, out IPropertyStore store).Failed)
            return null;
        store.GetValue(AppUserModelId, out var value);
        try
        {
            if (PInvoke.PropVariantToStringAlloc(value, out var text).Failed)
                return null;
            var result = text.ToString();
            PInvoke.CoTaskMemFree(text);
            return string.IsNullOrEmpty(result) ? null : result;
        }
        finally
        {
            PInvoke.PropVariantClear(ref value);
        }
    }
}
