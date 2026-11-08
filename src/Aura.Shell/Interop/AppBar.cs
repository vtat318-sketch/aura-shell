using System.Runtime.InteropServices;
using Aura.Shell.Services;

namespace Aura.Shell.Interop;

/// <summary>Registers our bar as a Windows "app bar" so maximized windows stop above it.</summary>
internal sealed class AppBar : IDisposable
{
    private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    private const uint ABE_BOTTOM = 3;
    public const int ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;

    private readonly IntPtr _hwnd;
    private bool _registered;

    public uint CallbackMessage { get; }

    public AppBar(IntPtr hwnd)
    {
        _hwnd = hwnd;
        CallbackMessage = Native.RegisterWindowMessage("AuraShell.AppBar");
    }

    private APPBARDATA Data() => new() { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };

    private void Register()
    {
        if (_registered) return;
        var d = Data();
        d.uCallbackMessage = CallbackMessage;
        Native.SHAppBarMessage(ABM_NEW, ref d);
        _registered = true;
    }

    /// <summary>Reserves <paramref name="reservePx"/> physical pixels at the bottom of the monitor.</summary>
    public RECT ReserveBottom(RECT monitor, int reservePx)
    {
        Register();
        var d = Data();
        d.uEdge = ABE_BOTTOM;
        d.rc = new RECT(monitor.Left, monitor.Bottom - reservePx, monitor.Right, monitor.Bottom);
        Native.SHAppBarMessage(ABM_QUERYPOS, ref d);
        d.rc.Top = d.rc.Bottom - reservePx;
        Native.SHAppBarMessage(ABM_SETPOS, ref d);
        return d.rc;
    }

    public void Dispose()
    {
        if (!_registered) return;
        var d = Data();
        Native.SHAppBarMessage(ABM_REMOVE, ref d);
        _registered = false;
    }
}

/// <summary>Hides / restores the stock Windows taskbar. Always restorable: on exit, on crash, or with `Aura.exe --restore`.</summary>
internal static class NativeTaskbar
{
    private const uint ABM_GETSTATE = 4, ABM_SETSTATE = 10;
    private const int ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;

    private static int? _originalState;
    public static bool IsHidden { get; private set; }
    /// <summary>State saved in the restore point: survives crashes and works from `Aura.exe --restore`.</summary>
    public static int? SavedState { get; set; }

    private static IEnumerable<IntPtr> TrayWindows()
    {
        var primary = TrayService.ExplorerTray();
        if (primary != IntPtr.Zero) yield return primary;
        IntPtr h = IntPtr.Zero;
        while ((h = Native.FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            yield return h;
    }

    public static bool IsTrayWindow(IntPtr hwnd)
    {
        var cls = Native.GetClass(hwnd);
        return cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    private static APPBARDATA Data(IntPtr hwnd) => new() { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd };

    public static void Hide()
    {
        var tray = TrayService.ExplorerTray();
        if (tray == IntPtr.Zero) return;
        var d = Data(tray);
        _originalState ??= (int)Native.SHAppBarMessage(ABM_GETSTATE, ref d).ToUInt32();
        d.lParam = new IntPtr(ABS_AUTOHIDE); // frees the work area the stock taskbar reserved
        Native.SHAppBarMessage(ABM_SETSTATE, ref d);
        foreach (var w in TrayWindows()) Native.ShowWindow(w, Native.SW_HIDE);
        IsHidden = true;
    }

    /// <summary>Current auto-hide state of the stock taskbar (ABS_* flags), stored in the restore point.</summary>
    public static int GetState()
    {
        if (IsHidden && _originalState is int s) return s;
        var tray = TrayService.ExplorerTray();
        if (tray == IntPtr.Zero) return ABS_ALWAYSONTOP;
        var d = Data(tray);
        return (int)Native.SHAppBarMessage(ABM_GETSTATE, ref d).ToUInt32();
    }

    public static void Restore()
    {
        var tray = TrayService.ExplorerTray();
        if (tray != IntPtr.Zero)
        {
            var d = Data(tray);
            d.lParam = new IntPtr(_originalState ?? SavedState ?? ABS_ALWAYSONTOP);
            Native.SHAppBarMessage(ABM_SETSTATE, ref d);
            foreach (var w in TrayWindows()) Native.ShowWindow(w, Native.SW_SHOWNA);
        }
        IsHidden = false;
    }

    /// <summary>Explorer sometimes re-shows its taskbar (e.g. when Start opens). Called from the WinEvent hook.</summary>
    public static void OnWindowShown(IntPtr hwnd)
    {
        if (IsHidden && IsTrayWindow(hwnd)) Native.ShowWindow(hwnd, Native.SW_HIDE);
    }
}
