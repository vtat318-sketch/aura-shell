using System.IO.Compression;

namespace Aura.Core;

/// <summary>
/// .aura package = zip with theme.json + assets. Treated as untrusted:
/// no executables, no path traversal, hard limits against zip bombs.
/// </summary>
public static class ThemePackage
{
    public const int MaxEntries = 64;
    public const long MaxEntryBytes = 200L * 1024 * 1024;   // single video
    public const long MaxTotalBytes = 300L * 1024 * 1024;   // whole package, uncompressed
    public const double MaxCompressionRatio = 100;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".json", ".png", ".jpg", ".jpeg", ".webp", ".mp4", ".webm" };

    public sealed record Result(Theme Theme, string Directory);

    /// <summary>Validates and extracts a package into <paramref name="themesRoot"/>/&lt;id&gt;.</summary>
    public static Result Install(string packagePath, string themesRoot)
    {
        using var zip = ZipFile.OpenRead(packagePath);
        if (zip.Entries.Count > MaxEntries) throw new ThemeValidationException($"Too many files (max {MaxEntries})");

        long total = 0;
        ZipArchiveEntry? themeEntry = null;
        foreach (var e in zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name)) continue; // directory
            if (!IsSafeRelativePath(e.FullName)) throw new ThemeValidationException($"Unsafe path: {e.FullName}");
            if (!AllowedExtensions.Contains(Path.GetExtension(e.Name))) throw new ThemeValidationException($"File type not allowed: {e.Name}");
            if (e.Length > MaxEntryBytes) throw new ThemeValidationException($"File too large: {e.Name}");
            if (e.CompressedLength > 0 && (double)e.Length / e.CompressedLength > MaxCompressionRatio)
                throw new ThemeValidationException($"Suspicious compression ratio: {e.Name}");
            total += e.Length;
            if (total > MaxTotalBytes) throw new ThemeValidationException("Package too large");
            if (e.FullName == "theme.json") themeEntry = e;
        }
        if (themeEntry is null) throw new ThemeValidationException("theme.json not found in package root");

        Theme theme;
        using (var s = themeEntry.Open())
        using (var ms = new MemoryStream())
        {
            CopyLimited(s, ms, ThemeLoader.MaxThemeJsonBytes);
            theme = ThemeLoader.Load(ms.ToArray());
        }

        var root = Path.GetFullPath(themesRoot);
        var dest = Path.GetFullPath(Path.Combine(root, theme.Id));
        if (!dest.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ThemeValidationException("Invalid theme id");

        var staging = dest + ".installing";
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        foreach (var e in zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name)) continue;
            var target = Path.GetFullPath(Path.Combine(staging, e.FullName));
            if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ThemeValidationException($"Unsafe path: {e.FullName}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var src = e.Open();
            using var dst = File.Create(target);
            CopyLimited(src, dst, MaxEntryBytes); // real size check, header Length can lie
        }

        if (theme.Wallpaper is { Type: not WallpaperType.Procedural } wp && !File.Exists(Path.Combine(staging, wp.Source)))
        {
            Directory.Delete(staging, true);
            throw new ThemeValidationException($"Wallpaper file missing: {wp.Source}");
        }

        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        Directory.Move(staging, dest);
        return new Result(theme, dest);
    }

    /// <summary>Creates a .aura package from a theme and optional asset files (relative name -> absolute path).</summary>
    public static void Create(Theme theme, string outputPath, IReadOnlyDictionary<string, string>? assets = null)
    {
        ThemeLoader.Validate(theme);
        using var fs = File.Create(outputPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("theme.json", CompressionLevel.Optimal);
        using (var w = new StreamWriter(entry.Open())) w.Write(ThemeLoader.Serialize(theme));
        foreach (var (name, path) in assets ?? new Dictionary<string, string>())
        {
            if (!IsSafeRelativePath(name) || !AllowedExtensions.Contains(Path.GetExtension(name)))
                throw new ThemeValidationException($"Asset not allowed: {name}");
            zip.CreateEntryFromFile(path, name, CompressionLevel.Optimal);
        }
    }

    public static bool IsSafeRelativePath(string p)
    {
        if (string.IsNullOrWhiteSpace(p) || p.Length > 260) return false;
        if (Path.IsPathRooted(p) || p.Contains(':') || p.StartsWith('/') || p.StartsWith('\\')) return false;
        var parts = p.Split('/', '\\');
        return parts.All(s => s.Length > 0 && s != "." && s != "..");
    }

    private static void CopyLimited(Stream src, Stream dst, long limit)
    {
        var buf = new byte[81920];
        long copied = 0;
        int n;
        while ((n = src.Read(buf, 0, buf.Length)) > 0)
        {
            copied += n;
            if (copied > limit) throw new ThemeValidationException("File exceeds size limit");
            dst.Write(buf, 0, n);
        }
    }
}
