using System.Text.Json;

namespace Aura.Shell.Services;

/// <summary>App-level settings (not part of a shareable theme). Stored in %AppData%\Aura\settings.json.</summary>
public sealed class AppSettings
{
    public bool HideNativeTaskbar { get; set; } = true;
    /// <summary>Aura's own minimize/restore animations when windows are toggled from the Aura bar.</summary>
    public bool WindowFx { get; set; } = true;
    /// <summary>Real acrylic blur behind the bar (only for the "full" layout).</summary>
    public bool AcrylicBlur { get; set; } = true;
    public List<string> Pinned { get; set; } = new();
    public string? ImageWallpaper { get; set; }
    public string? VideoWallpaper { get; set; }
    public bool ApplyThemeWallpaper { get; set; } = true;
    public bool FirstRun { get; set; } = true;
    public ExplorerSettings Explorer { get; set; } = new();
    private BarSettings _bar = new();
    /// <summary>Taskbar 2.0 behaviour (auto-hide, previews, hot corners, gestures).</summary>
    public BarSettings Bar { get => _bar; set => _bar = value ?? new(); }
    private TweakSettings _tweaks = new();
    /// <summary>Tray icons, admin mode, default folder handler, window borders, desktop menu.</summary>
    public TweakSettings Tweaks { get => _tweaks; set => _tweaks = value ?? new(); }

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aura");
    private static string FilePath => Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch { /* corrupted file -> defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch { /* best effort */ }
    }

    /// <summary>First-run pins: only apps that actually exist on this PC.</summary>
    public void EnsureDefaultPins()
    {
        if (!FirstRun) return;
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        {
            Path.Combine(win, "explorer.exe"),
            Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe"),
            Path.Combine(local, @"Microsoft\WindowsApps\wt.exe"),
            Path.Combine(pf86, @"Steam\steam.exe"),
            Path.Combine(local, @"Telegram Desktop\Telegram.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Telegram Desktop\Telegram.exe"),
            Path.Combine(local, @"Discord\Update.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c) && !Pinned.Contains(c, StringComparer.OrdinalIgnoreCase) && !c.EndsWith("Update.exe"))
                Pinned.Add(c);
        FirstRun = false;
        Save();
    }
}
