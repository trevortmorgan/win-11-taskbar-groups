using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace TaskbarGroups.Shell;

/// <summary>Icon pixels: 32-bit premultiplied BGRA, top row first.</summary>
public sealed record IconPixels(int Width, int Height, byte[] Bgra);

/// <summary>Gets the icon Windows itself shows for a file, folder, shortcut, URL or Store app.</summary>
public static class ShellIcons
{
    /// <summary>
    /// Loads the icon for <paramref name="parsingName"/> (a path or <c>shell:AppsFolder\&lt;AppID&gt;</c>) at exactly
    /// <paramref name="sizePx"/> pixels square. Windows picks the best source size and scales it itself, which looks
    /// sharper than scaling a stock size in XAML. Returns null if Windows can't resolve it. Call on an STA thread:
    /// some icon handlers require it.
    /// </summary>
    public static unsafe IconPixels? Load(string parsingName, int sizePx)
    {
        if (PInvoke.SHCreateItemFromParsingName<IShellItemImageFactory>(parsingName, null, out var factory).Failed)
            return null;

        DeleteObjectHBITMAPSafeHandle bitmap;
        try
        {
            factory.GetImage(new SIZE(sizePx, sizePx), SIIGBF.SIIGBF_ICONONLY, out bitmap);
        }
        catch (Exception)
        {
            return null; // the item exists but has no icon handler we can use
        }

        using (bitmap)
        {
            BITMAP info;
            if (PInvoke.GetObject((HGDIOBJ)bitmap.DangerousGetHandle(), sizeof(BITMAP), &info) == 0)
                return null;

            var (width, height) = (info.bmWidth, info.bmHeight);
            var header = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = width,
                    biHeight = -height, // negative = top-down rows
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = (uint)BI_COMPRESSION.BI_RGB,
                },
            };

            var pixels = new byte[width * height * 4];
            var dc = PInvoke.CreateCompatibleDC(HDC.Null);
            try
            {
                fixed (byte* p = pixels)
                {
                    if (PInvoke.GetDIBits(dc, bitmap, 0, (uint)height, p, &header, DIB_USAGE.DIB_RGB_COLORS) == 0)
                        return null;
                }
            }
            finally
            {
                PInvoke.DeleteDC(dc);
            }

            MakeOpaqueIfNoAlpha(pixels);
            return new IconPixels(width, height, pixels);
        }
    }

    /// <summary>Old-style icons without an alpha channel come back with alpha 0 everywhere, i.e. invisible.</summary>
    private static void MakeOpaqueIfNoAlpha(byte[] bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0)
                return;
        }
        for (var i = 3; i < bgra.Length; i += 4)
            bgra[i] = 0xFF;
    }
}
