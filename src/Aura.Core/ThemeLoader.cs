using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aura.Core;

public sealed class ThemeValidationException(string message) : Exception(message);

/// <summary>Loads and validates theme.json. Untrusted input: size-limited, strict, data-only.</summary>
public static partial class ThemeLoader
{
    public const int MaxThemeJsonBytes = 256 * 1024;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        WriteIndented = true,
    };

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,47}$")] private static partial Regex IdRegex();
    [GeneratedRegex("^\\d+\\.\\d+\\.\\d+$")] private static partial Regex VersionRegex();
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex HexRegex();
    [GeneratedRegex("^rgba\\(\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*,\\s*(0|1|0?\\.\\d+)\\s*\\)$")] private static partial Regex RgbaRegex();

    public static Theme Load(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length > MaxThemeJsonBytes)
            throw new ThemeValidationException($"theme.json is larger than {MaxThemeJsonBytes / 1024} KB");
        Theme? theme;
        try { theme = JsonSerializer.Deserialize<Theme>(utf8Json, JsonOptions); }
        catch (JsonException ex) { throw new ThemeValidationException($"Invalid theme.json: {ex.Message}"); }
        if (theme is null) throw new ThemeValidationException("theme.json is empty");
        Validate(theme);
        return theme;
    }

    public static Theme LoadFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxThemeJsonBytes)
            throw new ThemeValidationException($"theme.json is larger than {MaxThemeJsonBytes / 1024} KB");
        return Load(File.ReadAllBytes(path));
    }

    public static string Serialize(Theme theme) => JsonSerializer.Serialize(theme, JsonOptions);

    public static void Validate(Theme t)
    {
        Require(t.FormatVersion == 1, "formatVersion must be 1");
        Require(IdRegex().IsMatch(t.Id), "id must match ^[a-z0-9][a-z0-9-]{1,47}$");
        Require(t.Name.Length is >= 1 and <= 60, "name must be 1..60 chars");
        Require(t.Author.Length is >= 1 and <= 60, "author must be 1..60 chars");
        Require(VersionRegex().IsMatch(t.Version), "version must be semver x.y.z");
        Require(t.Tags.Count <= 8 && t.Tags.All(x => x.Length <= 24), "max 8 tags, 24 chars each");

        Hex(t.Palette.Accent, "palette.accent");
        Hex(t.Palette.SurfaceSolid, "palette.surfaceSolid");
        Hex(t.Palette.Text, "palette.text");
        Color(t.Palette.Surface, "palette.surface");
        Color(t.Palette.Tile, "palette.tile");
        Color(t.Palette.Border, "palette.border");

        var tb = t.Taskbar;
        Range(tb.Height, 40, 96, "taskbar.height");
        Range(tb.IconSize, 16, 48, "taskbar.iconSize");
        Range(tb.Radius, 0, 32, "taskbar.radius");
        Range(tb.Blur, 0, 48, "taskbar.blur");
        Range(t.Windows.DurationMs, 0, 1000, "windows.durationMs");

        if (t.Wallpaper is { } wp)
        {
            Require(wp.Source.Length is >= 1 and <= 260, "wallpaper.source must be 1..260 chars");
            if (wp.Type != WallpaperType.Procedural)
                Require(ThemePackage.IsSafeRelativePath(wp.Source), "wallpaper.source must be a safe relative path inside the package");
        }
    }

    private static void Require(bool ok, string message) { if (!ok) throw new ThemeValidationException(message); }
    private static void Range(int v, int min, int max, string name) => Require(v >= min && v <= max, $"{name} must be {min}..{max}");
    private static void Hex(string v, string name) => Require(HexRegex().IsMatch(v), $"{name} must be #RRGGBB");
    private static void Color(string v, string name)
    {
        var ok = HexRegex().IsMatch(v);
        if (!ok && RgbaRegex().Match(v) is { Success: true } m)
        {
            var parts = v[5..^1].Split(',');
            ok = parts.Take(3).All(p => int.Parse(p.Trim()) <= 255);
        }
        Require(ok, $"{name} must be #RRGGBB or rgba(r,g,b,a)");
    }
}
