using System.Runtime.InteropServices;

namespace Aura.Shell.Interop;

/// <summary>
/// Desktop icons through the desktop's own IFolderView2: icon size, auto-arrange / snap to grid, sorting,
/// show / hide and refresh — exactly what the stock "View" and "Sort by" submenus do.
/// </summary>
internal static class DesktopView
{
    public const uint FWF_AUTOARRANGE = 0x1, FWF_SNAPTOGRID = 0x4;
    public const uint PidName = 10, PidSize = 12, PidType = 4, PidDate = 14;

    private static readonly Guid SortFmtid = new("B725F130-47EF-101A-A5F1-02608C9EEBAC");
    private static readonly Guid ClsidShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid SidTopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IidShellBrowser = new("000214E2-0000-0000-C000-000000000046");
    private const uint WM_COMMAND = 0x0111;

    // ------------------------------------------------------------ windows

    public static IntPtr DefView()
    {
        var progman = Native.FindWindow("Progman", null);
        var view = progman == IntPtr.Zero ? IntPtr.Zero : Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        IntPtr worker = IntPtr.Zero;
        while (view == IntPtr.Zero && (worker = Native.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
            view = Native.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
        return view;
    }

    public static IntPtr ListView()
    {
        var view = DefView();
        return view == IntPtr.Zero ? IntPtr.Zero : Native.FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
    }

    public static void Refresh()
    {
        var view = DefView();
        if (view != IntPtr.Zero) PostMessage(view, WM_COMMAND, (IntPtr)0x7103, IntPtr.Zero);
    }

    public static bool IconsVisible
    {
        get
        {
            var list = ListView();
            return list != IntPtr.Zero && IsWindowVisible(list);
        }
    }

    public static void ToggleIcons()
    {
        var view = DefView();
        if (view != IntPtr.Zero) PostMessage(view, WM_COMMAND, (IntPtr)0x7402, IntPtr.Zero);
    }

    // ------------------------------------------------------------ IFolderView2

    public static int GetIconSize() => With(fv => { fv.GetViewModeAndIconSize(out _, out int size); return size; }, 48);

    public static void SetIconSize(int size) => With(fv => { fv.SetViewModeAndIconSize(1 /* FVM_ICON */, size); return true; }, false);

    public static bool GetFlag(uint flag) => With(fv => { fv.GetCurrentFolderFlags(out uint flags); return (flags & flag) != 0; }, false);

    public static void SetFlag(uint flag, bool on) => With(fv => { fv.SetCurrentFolderFlags(flag, on ? flag : 0); return true; }, false);

    public static (uint Pid, int Direction) CurrentSort() => With<(uint Pid, int Direction)>(fv =>
    {
        fv.GetSortColumnCount(out int count);
        if (count <= 0) return (PidName, 1);
        var columns = new SORTCOLUMN[count];
        fv.GetSortColumns(columns, count);
        return (columns[0].Pid, columns[0].Direction < 0 ? -1 : 1);
    }, (PidName, 1));

    /// <summary>Sort by a column; choosing the current column again flips the direction.</summary>
    public static void SortBy(uint pid)
    {
        var (current, direction) = CurrentSort();
        int next = current == pid ? -direction : 1;
        With(fv =>
        {
            var columns = new[] { new SORTCOLUMN { Fmtid = SortFmtid, Pid = pid, Direction = next } };
            fv.SetSortColumns(columns, 1);
            return true;
        }, false);
    }

    private static T With<T>(Func<IFolderView2, T> action, T fallback)
    {
        IFolderView2? view = null;
        try
        {
            view = FolderView();
            return view == null ? fallback : action(view);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return fallback;
        }
        finally
        {
            if (view != null) Marshal.ReleaseComObject(view);
        }
    }

    private static IFolderView2? FolderView()
    {
        var type = Type.GetTypeFromCLSID(ClsidShellWindows);
        if (type == null || Activator.CreateInstance(type) is not IShellWindows windows) return null;
        object location = 0; // CSIDL_DESKTOP
        object? root = null;
        var dispatch = windows.FindWindowSW(ref location, ref root, 8 /* SWC_DESKTOP */, out _, 1 /* SWFO_NEEDDISPATCH */);
        if (dispatch is not IComServiceProvider provider) return null;
        Guid sid = SidTopLevelBrowser, iid = IidShellBrowser;
        if (provider.QueryService(ref sid, ref iid, out object browserObject) != 0 || browserObject is not IShellBrowser browser) return null;
        if (browser.QueryActiveShellView(out object view) != 0) return null;
        return view as IFolderView2;
    }

    // ------------------------------------------------------------ COM

    [StructLayout(LayoutKind.Sequential)]
    private struct SORTCOLUMN
    {
        public Guid Fmtid;
        public uint Pid;
        public int Direction;
    }

    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IShellWindows
    {
        void D01(); void D02(); void D03(); void D04(); void D05(); void D06(); void D07(); void D08();
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object pvarLoc,
                            [MarshalAs(UnmanagedType.Struct)] ref object? pvarLocRoot,
                            int swClass, out int phwnd, int swfwOptions);
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IComServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
    }

    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void D01(); void D02(); void D03(); void D04(); void D05(); void D06();
        void D07(); void D08(); void D09(); void D10(); void D11(); void D12();
        [PreserveSig]
        int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object ppshv);
    }

    [ComImport, Guid("1af3a467-214f-4298-908e-06b03e0b39f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFolderView2
    {
        // IFolderView
        void GetCurrentViewMode(); void SetCurrentViewMode(); void GetFolder(); void Item(); void ItemCount(); void Items();
        void GetSelectionMarkedItem(); void GetFocusedItem(); void GetItemPosition(); void GetSpacing(); void GetDefaultSpacing();
        void GetAutoArrange(); void SelectItem(); void SelectAndPositionItems();
        // IFolderView2
        void SetGroupBy(); void GetGroupBy(); void SetViewProperty(); void GetViewProperty(); void SetTileViewProperties();
        void SetExtendedTileViewProperties(); void SetText();
        void SetCurrentFolderFlags(uint dwMask, uint dwFlags);
        void GetCurrentFolderFlags(out uint pdwFlags);
        void GetSortColumnCount(out int pcColumns);
        void SetSortColumns([In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] SORTCOLUMN[] rgSortColumns, int cColumns);
        void GetSortColumns([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] SORTCOLUMN[] rgSortColumns, int cColumns);
        void GetItem(); void GetVisibleItem(); void GetSelectedItem(); void GetSelection(); void GetSelectionState();
        void InvokeVerbOnSelection();
        void SetViewModeAndIconSize(uint uViewMode, int iImageSize);
        void GetViewModeAndIconSize(out uint puViewMode, out int piImageSize);
    }

    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
}
