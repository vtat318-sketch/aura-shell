using System.Diagnostics;
using System.Windows;
using Aura.Shell.Interop;
using Aura.Shell.Services;
using Aura.Shell.Views;

namespace Aura.Shell;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private TaskbarWindow? _taskbar;
    private HubWindow? _hub;

    internal static AppSettings Settings { get; private set; } = null!;
    internal static ThemeService Themes { get; private set; } = null!;
    internal static WindowTracker Tracker { get; private set; } = null!;
    internal static WallpaperService Wallpapers { get; private set; } = null!;
    internal static MinimizeFx Fx { get; private set; } = null!;
    internal static SystemVolume Volume { get; private set; } = null!;
    internal static ExplorerService Explorer { get; private set; } = null!;
    internal static TrayService? Tray { get; private set; }
    internal static WindowStyler Styler { get; private set; } = null!;
    internal static DesktopMenuService DesktopMenu { get; private set; } = null!;
    internal static ExplorerInterceptor ExplorerHook { get; private set; } = null!;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Emergency switches, they work even if the rest of Aura is broken:
        //   --show-taskbar  only bring the stock taskbar back (build.ps1 uses it after closing Aura)
        //   --restore       stock taskbar, wallpaper, Explorer menu entry, autostart -> as before Aura
        //   --reset         the same + Aura's settings moved to a backup folder (restore-windows.bat)
        if (e.Args.Contains("--show-taskbar"))
        {
            RestorePoint.Load();
            NativeTaskbar.Restore();
            Shutdown();
            return;
        }
        if (e.Args.Contains("--restore") || e.Args.Contains("--reset"))
        {
            RestorePoint.RestoreWindows();
            if (e.Args.Contains("--reset"))
            {
                RestorePoint.ArchiveSettings(RestorePoint.BackupFolderName());
                RestorePoint.Forget();
            }
            Shutdown();
            return;
        }

        // Restarted as administrator: wait until the previous Aura has released the bar and the mutex.
        string? waitPid = ArgAfter(e.Args, "--wait-pid");
        if (int.TryParse(waitPid, out int oldPid))
        {
            try { using var old = Process.GetProcessById(oldPid); old.WaitForExit(8000); }
            catch { /* already gone */ }
        }

        try { _mutex = new Mutex(true, "AuraShell.SingleInstance", out _ownsMutex); }
        catch (UnauthorizedAccessException)
        {
            // The running Aura is elevated: its mutex is closed to us, so it is "already running".
            _mutex = null;
            _ownsMutex = false;
        }
        string? openPath = ExplorerService.NormalizeOpenPath(ArgAfter(e.Args, "--open"));
        if (!_ownsMutex)
        {
            // Aura is already running: hand the folder over (context menu / shortcut) and quit.
            if (openPath != null) ExplorerService.TrySendOpen(openPath);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) => { Log(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => { Log(ex.ExceptionObject as Exception); SafeRestore(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SafeRestore();

        // Snapshot Windows (wallpaper, stock taskbar mode) before Aura changes anything.
        RestorePoint.CaptureOnce();

        Settings = AppSettings.Load();
        Settings.Tweaks.Clamp();

        // "Запускать Aura от администратора": hand over to an elevated copy (it waits for this one to exit).
        if (Settings.Tweaks.RunAsAdmin && !WindowActivator.AuraIsElevated && waitPid == null && !e.Args.Contains("--no-elevate")
            && StartElevated(e.Args))
        {
            Shutdown();
            return;
        }

        bool firstRun = Settings.FirstRun;
        Settings.EnsureDefaultPins();

        Themes = new ThemeService();
        Tracker = new WindowTracker(Settings);
        Wallpapers = new WallpaperService(Themes);
        Fx = new MinimizeFx();
        Volume = new SystemVolume();
        Explorer = new ExplorerService();
        Explorer.Init();

        Wallpapers.SyncKey(Themes.Current); // never change the wallpaper just because Aura started
        Themes.Changed += t => { if (Settings.ApplyThemeWallpaper) Wallpapers.ApplyTheme(t); };
        Tracker.ForegroundChanged += hwnd => Fx.Forget(hwnd);

        _taskbar = new TaskbarWindow();
        _taskbar.Show();
        Tracker.Start();

        Styler = new WindowStyler();
        DesktopMenu = new DesktopMenuService();
        ExplorerHook = new ExplorerInterceptor();
        Themes.Changed += _ => Styler.Apply();
        SafeStart(Styler.Apply);
        SafeStart(DesktopMenu.Apply);
        SafeStart(ExplorerHook.Apply);
        SafeStart(ApplyTraySetting);

        if (!string.IsNullOrEmpty(Settings.VideoWallpaper)) Wallpapers.SetVideo(Settings.VideoWallpaper);
        if (firstRun) ShowHub("presets");
        if (openPath != null) Explorer.OpenRequest(openPath);
    }

    private static void SafeStart(Action start)
    {
        try { start(); }
        catch (Exception ex) { Log(ex); }
    }

    /// <summary>Turns the Aura notification area (tray icons on the Aura bar) on or off.</summary>
    internal void ApplyTraySetting()
    {
        bool want = Settings.Tweaks.TrayIcons;
        if (want && Tray == null)
        {
            var tray = new TrayService();
            if (tray.Start()) Tray = tray;
            else tray.Dispose();
        }
        else if (!want && Tray != null)
        {
            Tray.Dispose();
            Tray = null;
        }
        TrayService.RaiseChanged();
    }

    /// <summary>Restarts Aura as administrator (Windows asks for permission). Nothing happens if the user says no.</summary>
    internal bool RestartElevated()
    {
        if (!StartElevated(Array.Empty<string>())) return false;
        Shutdown();
        return true;
    }

    private static bool StartElevated(string[] args)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;
        var parts = args.Select(a => a.Length == 0 || a.Contains(' ') ? "\"" + a.TrimEnd('\\') + "\"" : a).ToList();
        parts.Add("--wait-pid");
        parts.Add(Environment.ProcessId.ToString());
        try
        {
            Process.Start(new ProcessStartInfo(exe, string.Join(" ", parts)) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Exception ex)
        {
            Log(ex); // UAC prompt cancelled
            return false;
        }
    }

    internal void ShowHub(string tab = "presets")
    {
        if (_hub == null)
        {
            _hub = new HubWindow();
            _hub.Closed += (_, _) => _hub = null;
            _hub.Show();
        }
        if (_hub.WindowState == WindowState.Minimized) _hub.WindowState = WindowState.Normal;
        _hub.SelectTab(tab);
        _hub.Activate();
    }

    internal void ApplyNativeTaskbarSetting()
    {
        if (Settings.HideNativeTaskbar) NativeTaskbar.Hide(); else NativeTaskbar.Restore();
    }

    internal void Quit() => Shutdown();

    internal static bool ResetOnExit { get; private set; }
    private string? _resetBackup;

    /// <summary>The big "Вернуть всё как было" button: asks first, puts Windows back, archives Aura's settings and quits.</summary>
    internal void ConfirmAndResetEverything(Window? owner)
    {
        const string caption = "Вернуть всё как было";
        const string text =
            "Aura вернёт Windows к виду до первого запуска:\n\n" +
            "• стандартная панель задач Windows\n" +
            "• твои прежние обои\n" +
            "• без пункта «Открыть в Aura» в меню проводника\n" +
            "• без автозапуска Aura\n\n" +
            "Все настройки Aura сбросятся (копия сохранится в отдельную папку), и Aura закроется.\n\nПродолжить?";
        var answer = owner != null
            ? MessageBox.Show(owner, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try { Wallpapers?.StopVideo(restoreStatic: false); }
        catch (Exception ex) { Log(ex); }
        RestorePoint.RestoreWindows();
        _resetBackup = RestorePoint.BackupFolderName();
        ResetOnExit = true;
        MessageBox.Show("Готово: Windows выглядит как раньше, Aura сейчас закроется.\n\nКопия настроек Aura:\n" + _resetBackup,
            caption, MessageBoxButton.OK, MessageBoxImage.Information);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Tracker?.Dispose();
            DesktopMenu?.Dispose();
            Styler?.Dispose();
            ExplorerHook?.Dispose();
            Explorer?.Dispose();
            if (ResetOnExit) Wallpapers?.StopVideo(restoreStatic: false); else Wallpapers?.Dispose();
            _taskbar?.ReleaseAppBar();
            Tray?.Dispose(); // tray icons go back to the Windows taskbar
            Tray = null;
            Settings?.Save();
        }
        catch (Exception ex) { Log(ex); }
        SafeRestore();
        if (ResetOnExit && _resetBackup != null)
        {
            RestorePoint.RestoreWallpaper(); // once more, after every Aura timer has stopped
            RestorePoint.ArchiveSettings(_resetBackup);
            RestorePoint.Forget();
        }
        if (_ownsMutex) _mutex?.ReleaseMutex();
        base.OnExit(e);
    }

    private static void SafeRestore()
    {
        try { if (NativeTaskbar.IsHidden) NativeTaskbar.Restore(); }
        catch { /* nothing else we can do */ }
    }

    private static string? ArgAfter(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        if (i < 0) return null;
        return i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]) ? args[i + 1].Trim('"') : "";
    }

    internal static void Log(Exception? ex)
    {
        if (ex == null) return;
        try
        {
            Directory.CreateDirectory(AppSettings.Dir);
            File.AppendAllText(Path.Combine(AppSettings.Dir, "aura.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }
}
