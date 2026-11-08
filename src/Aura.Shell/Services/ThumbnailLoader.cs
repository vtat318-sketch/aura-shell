using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aura.Shell.Interop;

namespace Aura.Shell.Services;

/// <summary>
/// Real Windows thumbnails and icons (the same ones Explorer shows: photos, videos, PDFs, album art, app icons)
/// loaded on two background STA threads. Newest requests first, stale folders skipped, small bounded cache.
/// </summary>
public static class ThumbnailLoader
{
    private sealed record Job(string Path, string Key, int Size, bool IconOnly, int Generation, Action<ImageSource?> Done, Dispatcher Dispatcher);

    private static readonly BlockingCollection<Job> Queue = new(new ConcurrentStack<Job>()); // LIFO = what you scrolled to loads first
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private static bool _started;
    private static int _generation;
    private const int MaxCache = 420;

    private static readonly HashSet<string> MediaExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp", ".heic", ".heif", ".avif", ".tif", ".tiff", ".ico", ".svg", ".psd",
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp",
        ".mp3", ".flac", ".m4a", ".pdf",
    };

    private static readonly HashSet<string> UniqueIconExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".ico", ".url", ".msc", ".cpl", ".scr", ".appref-ms",
    };

    public static bool IsMedia(string path) => MediaExt.Contains(Path.GetExtension(path));

    /// <summary>Call when the visible folder changes; queued work for the old folder is dropped.</summary>
    public static void NextGeneration() => Interlocked.Increment(ref _generation);

    public static void ClearCache() => Cache.Clear();

    /// <param name="sizePx">Size in device pixels (already multiplied by DPI scale).</param>
    /// <param name="stamp">Last-write ticks so edited photos get a fresh thumbnail.</param>
    public static void Request(string path, bool isDir, int sizePx, bool thumbnails, long stamp, Action<ImageSource?> done)
    {
        string ext = Path.GetExtension(path);
        bool iconOnly = isDir || !thumbnails || !MediaExt.Contains(ext);
        string key = isDir ? $"dir|{path}|{sizePx}"
            : !iconOnly ? $"{path}|{sizePx}|{stamp}"
            : UniqueIconExt.Contains(ext) || ext.Length == 0 ? $"{path}|{sizePx}|i"
            : $"ext|{ext}|{sizePx}";

        if (Cache.TryGetValue(key, out var hit)) { done(hit); return; }
        EnsureStarted();
        Queue.Add(new Job(path, key, sizePx, iconOnly, _generation, done, Dispatcher.CurrentDispatcher));
    }

    private static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_started) return;
            _started = true;
            for (int i = 0; i < 2; i++)
            {
                var t = new Thread(Worker) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = $"Aura thumbnails {i}" };
                t.SetApartmentState(ApartmentState.STA); // shell COM objects want STA
                t.Start();
            }
        }
    }

    private static void Worker()
    {
        foreach (var job in Queue.GetConsumingEnumerable())
        {
            if (job.Generation != Volatile.Read(ref _generation)) continue;
            if (!Cache.TryGetValue(job.Key, out var img))
            {
                img = Load(job.Path, job.Size, job.IconOnly);
                if (Cache.Count > MaxCache) Cache.Clear();
                Cache[job.Key] = img;
            }
            var done = job.Done;
            var result = img;
            try { job.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => done(result))); }
            catch { /* window closed */ }
        }
    }

    private static ImageSource? Load(string path, int size, bool iconOnly)
    {
        ShellNative.IShellItemImageFactory? factory = null;
        IntPtr hbmp = IntPtr.Zero;
        try
        {
            ShellNative.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(ShellNative.IShellItemImageFactory).GUID, out factory);
            int flags = ShellNative.SIIGBF_BIGGERSIZEOK | (iconOnly ? ShellNative.SIIGBF_ICONONLY : 0);
            if (factory.GetImage(new ShellNative.SIZE(size, size), flags, out hbmp) != 0 || hbmp == IntPtr.Zero) return null;
            return ToBitmap(hbmp);
        }
        catch { return null; }
        finally
        {
            if (hbmp != IntPtr.Zero) ShellNative.DeleteObject(hbmp);
            if (factory != null) Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>Copies a 32-bit DIB keeping its alpha channel (Imaging.CreateBitmapSourceFromHBitmap would drop it).</summary>
    private static BitmapSource? ToBitmap(IntPtr hbmp)
    {
        if (ShellNative.GetObject(hbmp, Marshal.SizeOf<ShellNative.DIBSECTION>(), out var ds) == 0) return null;
        var bm = ds.dsBm;
        if (bm.bmBits == IntPtr.Zero || bm.bmBitsPixel != 32)
        {
            var fallback = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            fallback.Freeze();
            return fallback;
        }

        int w = bm.bmWidth, h = Math.Abs(bm.bmHeight), stride = bm.bmWidthBytes;
        if (w <= 0 || h <= 0 || stride < w * 4) return null;
        var src = new byte[stride * h];
        Marshal.Copy(bm.bmBits, src, 0, src.Length);

        bool hasAlpha = false;
        for (int i = 3; i < src.Length; i += 4) if (src[i] != 0) { hasAlpha = true; break; }
        if (!hasAlpha) for (int i = 3; i < src.Length; i += 4) src[i] = 255; // old-style opaque bitmap

        byte[] pixels = src;
        if (ds.dsBmih.biHeight > 0) // bottom-up DIB: flip rows
        {
            pixels = new byte[src.Length];
            for (int y = 0; y < h; y++) Buffer.BlockCopy(src, y * stride, pixels, (h - 1 - y) * stride, stride);
        }

        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }
}
