namespace Aura.Shell.Services;

public enum MenuAnimation { None, Fade, Pop, Slide }
public enum MenuDensity { Compact, Normal, Roomy }
public enum CornerStyle { Default, Square, Round, Small }

/// <summary>A user command in the Aura desktop menu ("Блокнот", "Steam", a script...).</summary>
public sealed class MenuCommand
{
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
    public string Args { get; set; } = "";
    public string Glyph { get; set; } = "\uE756";
}

/// <summary>Look and content of the Aura desktop right-click menu.</summary>
public sealed class DesktopMenuSettings
{
    public bool Enabled { get; set; }
    public int Radius { get; set; } = 14;
    public int Opacity { get; set; } = 78;          // % of the theme surface over the blurred wallpaper
    public bool Blur { get; set; } = true;
    public int BlurAmount { get; set; } = 30;
    public MenuDensity Density { get; set; } = MenuDensity.Normal;
    public bool Icons { get; set; } = true;
    public int FontSize { get; set; } = 13;
    public int Width { get; set; } = 270;
    public MenuAnimation Animation { get; set; } = MenuAnimation.Pop;
    public int AnimationMs { get; set; } = 170;
    public bool AccentHover { get; set; } = true;
    public bool Shadow { get; set; } = true;
    public List<string> Hidden { get; set; } = new();
    public List<MenuCommand> Custom { get; set; } = new();

    public void Clamp()
    {
        Radius = Math.Clamp(Radius, 0, 28);
        Opacity = Math.Clamp(Opacity, 30, 100);
        BlurAmount = Math.Clamp(BlurAmount, 0, 80);
        FontSize = Math.Clamp(FontSize, 11, 18);
        Width = Math.Clamp(Width, 200, 380);
        AnimationMs = Math.Clamp(AnimationMs, 0, 500);
        Hidden ??= new();
        Custom ??= new();
    }
}

/// <summary>System-wide features: tray icons, admin mode, default folder handler, window borders.</summary>
public sealed class TweakSettings
{
    /// <summary>Aura becomes the notification area: tray icons (VPN, Discord, Steam...) live on the Aura bar.</summary>
    public bool TrayIcons { get; set; } = true;
    public int TrayInline { get; set; } = 8;
    /// <summary>Restart Aura elevated so it can restore windows of apps running as administrator.</summary>
    public bool RunAsAdmin { get; set; }
    /// <summary>Every folder in Windows (desktop, Start, "show in folder"...) opens in Aura Explorer.</summary>
    public bool OpenFoldersInAura { get; set; }
    public bool WinEOpensAura { get; set; }
    /// <summary>Folders that programs open straight in Windows Explorer move into Aura Explorer too.</summary>
    public bool CatchExplorerWindows { get; set; } = true;
    /// <summary>A window that ignores restore (elevated, hidden owner) is opened through its tray icon instead.</summary>
    public bool TrayRestoreFallback { get; set; } = true;

    public bool AccentBorder { get; set; }
    public string BorderColor { get; set; } = "";   // "" = accent of the current theme
    public bool HideInactiveBorder { get; set; }
    public CornerStyle Corners { get; set; } = CornerStyle.Default;
    public bool DarkTitleBars { get; set; }

    private DesktopMenuSettings _menu = new();
    public DesktopMenuSettings DesktopMenu { get => _menu; set => _menu = value ?? new(); }

    public void Clamp()
    {
        TrayInline = Math.Clamp(TrayInline, 0, 16);
        BorderColor ??= "";
        DesktopMenu.Clamp();
    }
}
