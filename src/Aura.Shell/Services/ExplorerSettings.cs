using System.Text.Json.Serialization;

namespace Aura.Shell.Services;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExplorerView { Grid, Tiles, List }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FolderStyle { Accent, Rainbow, System }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExplorerBackdrop { Mica, Acrylic, Tabbed, Solid }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortField { Name, Modified, Size, Type }

/// <summary>Everything the user can tune in Aura Explorer. Stored inside settings.json.</summary>
public sealed class ExplorerSettings
{
    public ExplorerView View { get; set; } = ExplorerView.Grid;
    public int IconSize { get; set; } = 72;            // 24..160
    public int Spacing { get; set; } = 8;              // 2..20
    public int ItemRadius { get; set; } = 12;          // 0..24
    public double FontSize { get; set; } = 13;         // 11..17
    public FolderStyle Folders { get; set; } = FolderStyle.Accent;
    public ExplorerBackdrop Backdrop { get; set; } = ExplorerBackdrop.Mica;
    public double TintOpacity { get; set; } = 0.55;    // theme tint over Mica/Acrylic, 0..1
    public SortField SortBy { get; set; } = SortField.Name;
    public bool SortDescending { get; set; }
    public bool FoldersFirst { get; set; } = true;
    public bool ShowHidden { get; set; }
    public bool ShowExtensions { get; set; } = true;
    public bool ShowPreview { get; set; } = true;
    public bool ShowSidebar { get; set; } = true;
    public bool Thumbnails { get; set; } = true;
    public bool Animations { get; set; } = true;
    public bool ConfirmDelete { get; set; }            // Recycle Bin is undoable, so no nagging by default
    public bool Hotkey { get; set; } = true;           // Win+Alt+E opens Aura Explorer
    public bool ContextMenu { get; set; }              // "Открыть в Aura" inside Windows Explorer
    public bool ReplacePinnedExplorer { get; set; } = true; // the Explorer pin on Aura's taskbar opens Aura Explorer
    public double SidebarWidth { get; set; } = 232;
    public double PreviewWidth { get; set; } = 300;
    public List<string> Favorites { get; set; } = new();
    public List<string> LastTabs { get; set; } = new();

    public void Clamp()
    {
        IconSize = Math.Clamp(IconSize, 24, 160);
        Spacing = Math.Clamp(Spacing, 2, 20);
        ItemRadius = Math.Clamp(ItemRadius, 0, 24);
        FontSize = Math.Clamp(FontSize, 11, 17);
        TintOpacity = Math.Clamp(TintOpacity, 0, 1);
        SidebarWidth = Math.Clamp(SidebarWidth, 170, 420);
        PreviewWidth = Math.Clamp(PreviewWidth, 220, 520);
        Favorites ??= new();
        LastTabs ??= new();
    }

    /// <summary>Ready-made looks. Each one only touches visual settings, never the user's favorites or behaviour.</summary>
    public static readonly ExplorerPreset[] Presets =
    {
        new("aura", "Aura", "\uE790", "Сетка, фиолетовые папки, Mica", s =>
        {
            s.View = ExplorerView.Grid; s.IconSize = 72; s.Spacing = 8; s.ItemRadius = 12; s.FontSize = 13;
            s.Folders = FolderStyle.Accent; s.Backdrop = ExplorerBackdrop.Mica; s.TintOpacity = 0.55;
            s.ShowPreview = true; s.ShowSidebar = true; s.Thumbnails = true;
        }),
        new("gallery", "Галерея", "\uEB9F", "Крупные миниатюры для фото и видео", s =>
        {
            s.View = ExplorerView.Grid; s.IconSize = 144; s.Spacing = 12; s.ItemRadius = 16; s.FontSize = 13;
            s.Folders = FolderStyle.Accent; s.Backdrop = ExplorerBackdrop.Tabbed; s.TintOpacity = 0.45;
            s.ShowPreview = false; s.ShowSidebar = true; s.Thumbnails = true;
        }),
        new("classic", "Классика", "\uE8FD", "Таблица как в Windows, всё видно сразу", s =>
        {
            s.View = ExplorerView.List; s.IconSize = 24; s.Spacing = 2; s.ItemRadius = 4; s.FontSize = 13;
            s.Folders = FolderStyle.System; s.Backdrop = ExplorerBackdrop.Solid; s.TintOpacity = 1;
            s.ShowPreview = true; s.ShowSidebar = true;
        }),
        new("compact", "Компакт", "\uE80A", "Плитки, много файлов на экране", s =>
        {
            s.View = ExplorerView.Tiles; s.IconSize = 40; s.Spacing = 4; s.ItemRadius = 8; s.FontSize = 12;
            s.Folders = FolderStyle.Accent; s.Backdrop = ExplorerBackdrop.Acrylic; s.TintOpacity = 0.4;
            s.ShowPreview = false; s.ShowSidebar = true;
        }),
        new("rainbow", "Радуга", "\uE771", "Каждая папка своего цвета", s =>
        {
            s.View = ExplorerView.Grid; s.IconSize = 84; s.Spacing = 10; s.ItemRadius = 18; s.FontSize = 13;
            s.Folders = FolderStyle.Rainbow; s.Backdrop = ExplorerBackdrop.Mica; s.TintOpacity = 0.5;
            s.ShowPreview = true; s.ShowSidebar = true;
        }),
        new("focus", "Фокус", "\uE740", "Только файлы: без боковых панелей", s =>
        {
            s.View = ExplorerView.Grid; s.IconSize = 64; s.Spacing = 10; s.ItemRadius = 14; s.FontSize = 13;
            s.Folders = FolderStyle.Accent; s.Backdrop = ExplorerBackdrop.Acrylic; s.TintOpacity = 0.3;
            s.ShowPreview = false; s.ShowSidebar = false;
        }),
    };
}

public sealed record ExplorerPreset(string Id, string Name, string Glyph, string Hint, Action<ExplorerSettings> Apply);
