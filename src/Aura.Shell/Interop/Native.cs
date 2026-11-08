using System.Runtime.InteropServices;
using System.Text;

namespace Aura.Shell.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
    public RECT(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
    public readonly bool IsEmpty => Width <= 0 || Height <= 0;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

[StructLayout(LayoutKind.Sequential)]
internal struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }

[StructLayout(LayoutKind.Sequential)]
internal struct ANIMATIONINFO { public uint cbSize; public int iMinAnimate; }

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_POWER_STATUS
{
    public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
    public int BatteryLifeTime, BatteryFullLifeTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AccentPolicy { public int AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }

[StructLayout(LayoutKind.Sequential)]
internal struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
internal delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

/// <summary>All Win32 / DWM / Shell interop in one place. Only documented APIs, except SetWindowCompositionAttribute (used by every acrylic app).</summary>
internal static class Native
{
    // window styles
    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_CHILD = 0x40000000, WS_POPUP = 0x80000000;
    public const long WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000,
                      WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000, WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    public const uint GW_OWNER = 4;

    // ShowWindow
    public const int SW_HIDE = 0, SW_SHOWNORMAL = 1, SW_SHOWMINIMIZED = 2, SW_SHOWNOACTIVATE = 4, SW_SHOW = 5, SW_MINIMIZE = 6, SW_SHOWNA = 8, SW_RESTORE = 9;

    // SetWindowPos
    public static readonly IntPtr HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2), HWND_BOTTOM = new(1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_FRAMECHANGED = 0x20;

    // messages
    public const uint WM_CLOSE = 0x0010, WM_SYSCOMMAND = 0x0112, WM_GETICON = 0x007F;
    public const int SC_MINIMIZE = 0xF020, SC_RESTORE = 0xF120, SC_CLOSE = 0xF060;
    public const int ICON_SMALL = 0, ICON_BIG = 1, ICON_SMALL2 = 2;
    public const int GCLP_HICON = -14, GCLP_HICONSM = -34;
    public const uint SMTO_ABORTIFHUNG = 0x2;

    // WinEvents
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_SYSTEM_MINIMIZESTART = 0x0016, EVENT_SYSTEM_MINIMIZEEND = 0x0017,
                      EVENT_OBJECT_CREATE = 0x8000, EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_HIDE = 0x8003,
                      EVENT_OBJECT_NAMECHANGE = 0x800C, EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0, WINEVENT_SKIPOWNPROCESS = 0x2;
    public const int OBJID_WINDOW = 0, CHILDID_SELF = 0;

    // DWM
    public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3, DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14,
                     DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // SystemParametersInfo
    public const uint SPI_SETDESKWALLPAPER = 0x0014, SPI_GETANIMATION = 0x0048, SPI_SETANIMATION = 0x0049;
    public const uint SPIF_UPDATEINIFILE = 0x1, SPIF_SENDCHANGE = 0x2;

    // monitors
    public const uint MONITOR_DEFAULTTONEAREST = 2, MONITOR_DEFAULTTOPRIMARY = 1;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const byte VK_MENU = 0x12, VK_LWIN = 0x5B;
    public const uint KEYEVENTF_KEYUP = 0x2;
    public const uint PW_RENDERFULLCONTENT = 0x2;

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern IntPtr GetClassLongPtr64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetClassLongW")] private static extern uint GetClassLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
    public static extern bool SystemParametersInfo(uint action, uint param, string pv, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    public static extern bool SystemParametersInfo(uint action, uint param, ref ANIMATIONINFO pv, uint winIni);

    [DllImport("user32.dll")] public static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHDefExtractIconW")]
    public static extern int SHDefExtractIcon(string file, int index, uint flags, out IntPtr large, out IntPtr small, uint size);

    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exe, ref int size);
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    // ---------- helpers ----------

    public static long GetWindowLong(IntPtr hwnd, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index).ToInt64() : GetWindowLong32(hwnd, index);

    public static void SetWindowLong(IntPtr hwnd, int index, long value)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, index, new IntPtr(value));
        else SetWindowLong32(hwnd, index, unchecked((int)value));
    }

    public static void AddExStyle(IntPtr hwnd, long flags) => SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | flags);

    public static IntPtr GetClassLongPtr(IntPtr hwnd, int index) =>
        IntPtr.Size == 8 ? GetClassLongPtr64(hwnd, index) : new IntPtr(GetClassLong32(hwnd, index));

    public static string GetTitle(IntPtr hwnd)
    {
        int len = GetWindowTextLength(hwnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string? GetProcessPath(uint pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    public static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    /// <summary>Visible window bounds without the invisible resize borders.</summary>
    public static RECT GetVisibleBounds(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0 && !r.IsEmpty) return r;
        GetWindowRect(hwnd, out r);
        return r;
    }

    public static MONITORINFO GetMonitor(IntPtr monitor)
    {
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref mi);
        return mi;
    }

    public static MONITORINFO PrimaryMonitor() => GetMonitor(MonitorFromPoint(new POINT(0, 0), MONITOR_DEFAULTTOPRIMARY));

    /// <summary>Real acrylic blur behind a window (Win10 1803+ / Win11). tint is AABBGGRR.</summary>
    public static void SetAcrylic(IntPtr hwnd, bool enable, uint tintAbgr)
    {
        var accent = new AccentPolicy { AccentState = enable ? 4 : 0, AccentFlags = 2, GradientColor = tintAbgr };
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr p = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, p, false);
            var data = new WindowCompositionAttributeData { Attribute = 19 /* WCA_ACCENT_POLICY */, Data = p, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    /// <summary>Reliably brings a foreign window to the front (the Alt-key trick unlocks SetForegroundWindow).</summary>
    /// <summary>Restore + focus with every fallback (tray-hidden apps, owned dialogs, admin apps). See WindowActivator.</summary>
    public static void Activate(IntPtr hwnd) => WindowActivator.Activate(hwnd);

    public static void PressWinKey()
    {
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static bool OnBattery() => GetSystemPowerStatus(out var s) && s.ACLineStatus == 0;

    public static int BatteryPercent() => GetSystemPowerStatus(out var s) && s.BatteryFlag != 128 && s.BatteryLifePercent <= 100 ? s.BatteryLifePercent : -1;

    /// <summary>Temporarily toggles the system minimize/restore animation for this session only (not persisted).</summary>
    public static bool SetSystemMinAnimation(bool enabled)
    {
        var ai = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>() };
        SystemParametersInfo(SPI_GETANIMATION, ai.cbSize, ref ai, 0);
        bool was = ai.iMinAnimate != 0;
        ai.iMinAnimate = enabled ? 1 : 0;
        SystemParametersInfo(SPI_SETANIMATION, ai.cbSize, ref ai, 0);
        return was;
    }
}
