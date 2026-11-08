using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aura.Shell.Interop;

namespace Aura.Shell.Services;

/// <summary>Extracts and caches app icons (48 px) and friendly names. Everything is frozen so it is cheap to share.</summary>
public static class IconCache
{
    private static readonly Dictionary<string, ImageSource?> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? ForFile(string path)
    {
        if (Icons.TryGetValue(path, out var cached)) return cached;
        ImageSource? img = null;
        try
        {
            uint size = (16u << 16) | 48u; // MAKELONG(large=48, small=16)
            if (Native.SHDefExtractIcon(path, 0, 0, out IntPtr large, out IntPtr small, size) == 0)
            {
                if (large != IntPtr.Zero) { img = FromHIcon(large); Native.DestroyIcon(large); }
                if (small != IntPtr.Zero) Native.DestroyIcon(small);
            }
        }
        catch { img = null; }
        Icons[path] = img;
        return img;
    }

    /// <summary>Icon the window itself advertises (used for UWP hosts and apps without file icons). Not cached: owned by the window.</summary>
    public static ImageSource? ForWindow(IntPtr hwnd)
    {
        try
        {
            Native.SendMessageTimeout(hwnd, Native.WM_GETICON, new IntPtr(Native.ICON_BIG), IntPtr.Zero, Native.SMTO_ABORTIFHUNG, 40, out IntPtr h);
            if (h == IntPtr.Zero)
                Native.SendMessageTimeout(hwnd, Native.WM_GETICON, new IntPtr(Native.ICON_SMALL2), IntPtr.Zero, Native.SMTO_ABORTIFHUNG, 40, out h);
            if (h == IntPtr.Zero) h = Native.GetClassLongPtr(hwnd, Native.GCLP_HICON);
            if (h == IntPtr.Zero) h = Native.GetClassLongPtr(hwnd, Native.GCLP_HICONSM);
            return h == IntPtr.Zero ? null : FromHIcon(h);
        }
        catch { return null; }
    }

    public static string NameFor(string path)
    {
        if (Names.TryGetValue(path, out var n)) return n;
        string name = Path.GetFileNameWithoutExtension(path);
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrWhiteSpace(info.FileDescription)) name = info.FileDescription!.Trim();
        }
        catch { /* keep file name */ }
        if (path.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase)) name = "Проводник";
        Names[path] = name;
        return name;
    }

    private static ImageSource FromHIcon(IntPtr hIcon)
    {
        var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }
}
