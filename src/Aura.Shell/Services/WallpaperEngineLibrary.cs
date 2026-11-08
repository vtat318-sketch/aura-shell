using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Aura.Shell.Services;

public sealed record WeWallpaper(string Title, string VideoPath, string? PreviewPath);

/// <summary>
/// Reads the user's OWN Wallpaper Engine subscriptions from disk (Steam Workshop folder, app 431960).
/// Nothing is downloaded or redistributed; only video wallpapers are supported (scene/web need WE's renderer).
/// </summary>
public static class WallpaperEngineLibrary
{
    public const string AppId = "431960";
    private static readonly string[] VideoExt = { ".mp4", ".m4v", ".mov", ".wmv", ".avi", ".mkv" };

    public static List<WeWallpaper> Scan()
    {
        var result = new List<WeWallpaper>();
        foreach (var lib in SteamLibraries())
        {
            string[] roots =
            {
                Path.Combine(lib, "steamapps", "workshop", "content", AppId),
                Path.Combine(lib, "steamapps", "common", "wallpaper_engine", "projects", "myprojects"),
            };
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                IEnumerable<string> dirs;
                try { dirs = Directory.EnumerateDirectories(root).ToList(); }
                catch { continue; }
                foreach (var dir in dirs)
                {
                    var w = TryRead(dir);
                    if (w != null) result.Add(w);
                }
            }
        }
        return result.DistinctBy(w => w.VideoPath, StringComparer.OrdinalIgnoreCase).OrderBy(w => w.Title).ToList();
    }

    public static bool SteamFound() => SteamLibraries().Any();

    private static List<string> SteamLibraries()
    {
        var libs = new List<string>();
        string? steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (string.IsNullOrWhiteSpace(steam)) return libs;
        steam = steam.Replace('/', '\\');
        libs.Add(steam);
        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        try
        {
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }
        catch { /* ignore */ }
        return libs.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static WeWallpaper? TryRead(string dir)
    {
        try
        {
            string pj = Path.Combine(dir, "project.json");
            if (!File.Exists(pj)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(pj),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            string type = Str(root, "type") ?? "";
            if (!type.Equals("video", StringComparison.OrdinalIgnoreCase)) return null;
            string? file = Str(root, "file");
            if (string.IsNullOrWhiteSpace(file)) return null;

            string baseDir = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
            string full = Path.GetFullPath(Path.Combine(dir, file));
            if (!full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null; // no path escape
            if (!VideoExt.Contains(Path.GetExtension(full).ToLowerInvariant())) return null;

            string title = Str(root, "title") ?? Path.GetFileName(dir);
            string? preview = Str(root, "preview");
            string? previewPath = null;
            if (!string.IsNullOrWhiteSpace(preview))
            {
                string pp = Path.GetFullPath(Path.Combine(dir, preview));
                if (pp.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) && File.Exists(pp)) previewPath = pp;
            }
            return new WeWallpaper(title, full, previewPath);
        }
        catch { return null; }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
