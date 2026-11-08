using System.Text.Json.Serialization;

namespace Aura.Core;

/// <summary>Theme format v1. Mirrors schema/theme.schema.json. Data only — no executable content.</summary>
public sealed record Theme
{
    [JsonPropertyName("$schema")] public string? Schema { get; init; }
    public int FormatVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    public required string Version { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required Palette Palette { get; init; }
    public TaskbarSettings Taskbar { get; init; } = new();
    public ClockSettings Clock { get; init; } = new();
    public WindowAnimations Windows { get; init; } = new();
    public WallpaperSettings? Wallpaper { get; init; }
    public PerformanceSettings Performance { get; init; } = new();
}

public sealed record Palette
{
    public required string Accent { get; init; }
    public required string Surface { get; init; }
    public required string SurfaceSolid { get; init; }
    public required string Text { get; init; }
    public string Tile { get; init; } = "rgba(255,255,255,0.07)";
    public string Border { get; init; } = "rgba(255,255,255,0.10)";
}

public enum TaskbarLayout { Islands, Island, Full }
public enum TaskbarAlignment { Center, Left }
public enum HoverEffect { Dock, Lift, None }
public enum IconStyle { Tile, Plain }
public enum OpenAnimation { Scale, Slide, Fade, None }
public enum MinimizeAnimation { Genie, Shrink, Fade, None }
public enum WallpaperType { Procedural, Image, Video, Web, Shader }

public sealed record TaskbarSettings
{
    public TaskbarLayout Layout { get; init; } = TaskbarLayout.Islands;
    public TaskbarAlignment Alignment { get; init; } = TaskbarAlignment.Center;
    public int Height { get; init; } = 60;
    public int IconSize { get; init; } = 28;
    public int Radius { get; init; } = 18;
    public int Blur { get; init; } = 24;
    public HoverEffect HoverEffect { get; init; } = HoverEffect.Dock;
    public IconStyle IconStyle { get; init; } = IconStyle.Tile;
    public bool ToolsGroup { get; init; } = true;
}

public sealed record ClockSettings
{
    public bool ShowSeconds { get; init; }
    public bool ShowDate { get; init; } = true;
}

public sealed record WindowAnimations
{
    public OpenAnimation Open { get; init; } = OpenAnimation.Scale;
    public MinimizeAnimation Minimize { get; init; } = MinimizeAnimation.Genie;
    public int DurationMs { get; init; } = 280;
}

public sealed record WallpaperSettings
{
    public required WallpaperType Type { get; init; }
    public required string Source { get; init; }
}

public sealed record PerformanceSettings
{
    public bool PauseOnFullscreen { get; init; } = true;
    public bool PauseOnBattery { get; init; } = true;
    public bool EcoMode { get; init; }
}
