using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Aura.Shell.Services;

namespace Aura.Shell.Interop;

/// <summary>
/// Brings any window to the front, including the ones a plain ShowWindow can't reach:
/// apps hidden to the tray, Delphi/WinForms apps with a hidden owner window, windows with an owned dialog,
/// and apps running as administrator (most VPN clients). Order of attempts:
/// 1. WM_SYSCOMMAND/SC_RESTORE (what the window's own title bar sends) → 2. ShowWindowAsync →
/// 3. focus the owned dialog / real main window → 4. click the app's tray icon → 5. offer admin mode.
/// </summary>
internal static class WindowActivator
{
    private const int SW_SHOW = 5, SW_RESTORE = 9, SW_MINIMIZE = 6, SW_MAXIMIZE = 3;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_RESTORE = 0xF120, SC_MINIMIZE = 0xF020, SC_MAXIMIZE = 0xF030;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2;
    private const uint GA_ROOTOWNER = 3, GW_OWNER = 4;

    private static bool _askedThisSession;

    /// <summary>True when Aura itself runs as administrator.</summary>
    public static bool AuraIsElevated { get; } = IsCurrentProcessElevated();

    public static bool Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
        if (IsIconic(hwnd)) Restore(hwnd);
        else if (!IsWindowVisible(hwnd)) ShowWindowAsync(hwnd, SW_SHOW);

        // Delphi/old WinForms: the taskbar button belongs to an invisible 0×0 owner; the real window is owned by it.
        IntPtr target = hwnd;
        if (IsPhantom(hwnd))
        {
            IntPtr owned = FindOwnedWindow(hwnd);
            if (owned != IntPtr.Zero)
            {
                if (IsIconic(owned)) Restore(owned);
                target = owned;
            }
        }

        // A window with an open dialog must hand the focus to that dialog, otherwise it "doesn't open".
        IntPtr popup = GetLastActivePopup(target);
        if (popup != IntPtr.Zero && popup != target && IsWindowVisible(popup) && IsWindowEnabled(popup)) target = popup;

        bool ok = ForceForeground(target);
        ScheduleCheck(hwnd, target);
        return ok;
    }

    /// <summary>Restores a minimized window the way its own title bar does, falling back to ShowWindow.</summary>
    public static void Restore(IntPtr hwnd)
    {
        SendMessageTimeout(hwnd, WM_SYSCOMMAND, new IntPtr(SC_RESTORE), IntPtr.Zero, SMTO_ABORTIFHUNG, 250, out _);
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, SW_RESTORE);
    }

    public static void Minimize(IntPtr hwnd)
    {
        if (!IsWindow(hwnd)) return;
        SendMessageTimeout(hwnd, WM_SYSCOMMAND, new IntPtr(SC_MINIMIZE), IntPtr.Zero, SMTO_ABORTIFHUNG, 250, out _);
        if (!IsIconic(hwnd)) ShowWindowAsync(hwnd, SW_MINIMIZE);
    }

    public static void Maximize(IntPtr hwnd)
    {
        if (!IsWindow(hwnd)) return;
        SendMessageTimeout(hwnd, WM_SYSCOMMAND, new IntPtr(SC_MAXIMIZE), IntPtr.Zero, SMTO_ABORTIFHUNG, 250, out _);
        if (!IsZoomed(hwnd)) ShowWindowAsync(hwnd, SW_MAXIMIZE);
        ForceForeground(hwnd);
    }

    /// <summary>«Снять задачу»: ends the process that owns the window. Returns false if Windows refused.</summary>
    public static bool Kill(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId) return false;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            p.Kill(entireProcessTree: false);
            return true;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return false;
        }
    }

    /// <summary>SetForegroundWindow with every fallback Windows allows.</summary>
    public static bool ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (GetForegroundWindow() == hwnd) return true;
        if (SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd)
        {
            BringWindowToTop(hwnd);
            return true;
        }

        IntPtr fg = GetForegroundWindow();
        uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out _) : 0;
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try
        {
            // Alt tap: Windows lets the process that just produced input take the foreground.
            keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            SetForegroundWindow(hwnd);
            keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            BringWindowToTop(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(me, fgThread, false);
        }
        if (GetForegroundWindow() != hwnd) SwitchToThisWindow(hwnd, true);
        return GetForegroundWindow() == hwnd;
    }

    private static void After(int ms, Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try { action(); }
            catch (Exception ex) { App.Log(ex); }
        };
        timer.Start();
    }

    private static bool IsFront(IntPtr hwnd, IntPtr target)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        if (fg == hwnd || fg == target) return true;
        IntPtr root = GetAncestor(fg, GA_ROOTOWNER);
        if (root == hwnd || root == target) return true;
        // same process counts too (the app may show a different window than the one we clicked)
        GetWindowThreadProcessId(fg, out uint a);
        GetWindowThreadProcessId(hwnd, out uint b);
        return a != 0 && a == b;
    }

    private static void ScheduleCheck(IntPtr hwnd, IntPtr target)
    {
        After(450, () =>
        {
            if (!IsWindow(hwnd)) return;
            bool stuck = IsIconic(hwnd) || (target != hwnd && IsWindow(target) && IsIconic(target));
            if (stuck)
            {
                // Some apps ignore ShowWindow but obey the command their own title bar sends.
                PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_RESTORE), IntPtr.Zero);
                SwitchToThisWindow(target, true);
            }
            if (!stuck && IsFront(hwnd, target)) return;

            // Last resort that works even for elevated apps: the tray icon (Windows lets tray callbacks through UIPI).
            if (App.Settings?.Tweaks.TrayRestoreFallback == true && ClickTrayIcon(hwnd))
            {
                After(700, () =>
                {
                    if (IsWindow(hwnd) && !IsIconic(hwnd) && IsFront(hwnd, target)) return;
                    if (!AuraIsElevated && IsElevated(hwnd)) OfferAdminMode(hwnd);
                });
                return;
            }
            if (!AuraIsElevated && IsElevated(hwnd)) OfferAdminMode(hwnd);
        });
    }

    // ------------------------------------------------------------ tray icon fallback

    /// <summary>The tray icon that belongs to the same process as <paramref name="hwnd"/>, if Aura hosts the tray.</summary>
    public static TrayIcon? TrayIconFor(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == 0 ? null : TrayIconForPid(pid, ExePath(pid));
    }

    private static TrayIcon? TrayIconForPid(uint pid, string? exe)
    {
        var tray = App.Tray;
        if (tray == null) return null;
        TrayIcon? byExe = null;
        foreach (var icon in tray.Icons.ToList())
        {
            if (!IsWindow(icon.Owner)) continue;
            GetWindowThreadProcessId(icon.Owner, out uint owner);
            if (owner == pid) return icon;
            if (byExe == null && exe != null && string.Equals(ExePath(owner), exe, StringComparison.OrdinalIgnoreCase)) byExe = icon;
        }
        return byExe;
    }

    /// <summary>Left click on the app's tray icon, then a double click if the window still isn't up.</summary>
    public static bool ClickTrayIcon(IntPtr hwnd)
    {
        var icon = TrayIconFor(hwnd);
        if (icon == null || App.Tray == null) return false;
        App.Tray.Click(icon, TrayClick.Left);
        After(400, () =>
        {
            if (IsWindow(hwnd) && IsWindowVisible(hwnd) && !IsIconic(hwnd) && IsFront(hwnd, hwnd)) return;
            App.Tray?.Click(icon, TrayClick.Double);
        });
        return true;
    }

    /// <summary>
    /// The pinned app is running but has no window on the taskbar (it lives in the tray, like most VPN clients):
    /// open it through its tray icon instead of starting a second copy. Returns false if there's no such icon.
    /// </summary>
    public static bool ShowFromTray(string exePath)
    {
        var tray = App.Tray;
        if (tray == null || App.Settings?.Tweaks.TrayRestoreFallback != true) return false;
        foreach (var icon in tray.Icons.ToList())
        {
            if (!IsWindow(icon.Owner)) continue;
            GetWindowThreadProcessId(icon.Owner, out uint pid);
            if (!string.Equals(ExePath(pid), exePath, StringComparison.OrdinalIgnoreCase)) continue;
            tray.Click(icon, TrayClick.Left);
            After(450, () =>
            {
                if (ProcessHasForeground(pid)) return;
                App.Tray?.Click(icon, TrayClick.Double);
            });
            return true;
        }
        return false;
    }

    private static bool ProcessHasForeground(uint pid)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        GetWindowThreadProcessId(fg, out uint p);
        return p == pid;
    }

    /// <summary>Full exe path of a process; works for elevated processes too (limited query right).</summary>
    public static string? ExePath(uint pid)
    {
        if (pid == 0) return null;
        IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(process, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(process); }
    }

    // ------------------------------------------------------------ hidden-owner apps

    /// <summary>Invisible or zero-sized "application" window that only carries the taskbar button.</summary>
    private static bool IsPhantom(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out RECT r)) return false;
        return r.Right - r.Left <= 1 || r.Bottom - r.Top <= 1 || !IsWindowVisible(hwnd);
    }

    private static IntPtr FindOwnedWindow(IntPtr owner)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((w, _) =>
        {
            if (GetWindow(w, GW_OWNER) != owner) return true;
            if (!IsWindowVisible(w) && !IsIconic(w)) return true;
            if (!GetWindowRect(w, out RECT r) || r.Right - r.Left < 40 || r.Bottom - r.Top < 40) return true;
            found = w;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    // ------------------------------------------------------------ admin mode

    private static void OfferAdminMode(IntPtr hwnd)
    {
        if (_askedThisSession || App.Settings == null) return;
        _askedThisSession = true;
        GetWindowThreadProcessId(hwnd, out uint pid);
        string name = ProcessName(pid);
        var answer = MessageBox.Show(
            $"«{name}» запущено от имени администратора, поэтому Windows не даёт обычным программам разворачивать его окно.\n\n" +
            "Перезапустить Aura с правами администратора? Тогда панель сможет управлять любыми окнами.\n\n" +
            "Выключить можно в хабе: Система → «Запускать Aura от администратора».",
            "Aura", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer != MessageBoxResult.Yes) return;
        App.Settings.Tweaks.RunAsAdmin = true;
        App.Settings.Save();
        if (!App.Current.RestartElevated())
        {
            App.Settings.Tweaks.RunAsAdmin = false;
            App.Settings.Save();
        }
    }

    private static string ProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return "Это приложение"; }
    }

    /// <summary>True when the window's process runs as administrator (or is protected so we can't even look).</summary>
    public static bool IsElevated(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId) return false;
        IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return true;
        try { return TokenIsElevated(process, assumeWhenDenied: true); }
        finally { CloseHandle(process); }
    }

    private static bool IsCurrentProcessElevated()
    {
        try { return TokenIsElevated(GetCurrentProcess(), assumeWhenDenied: false); }
        catch { return false; }
    }

    private static bool TokenIsElevated(IntPtr process, bool assumeWhenDenied)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) return assumeWhenDenied;
        IntPtr buffer = Marshal.AllocHGlobal(4);
        try
        {
            return GetTokenInformation(token, TokenElevation, buffer, 4, out _) && Marshal.ReadInt32(buffer) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            CloseHandle(token);
        }
    }

    // ------------------------------------------------------------ P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returnLength);
}
