using System.Runtime.InteropServices;

namespace Khefest.Windows.Native;

internal static unsafe partial class Win32Native
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";

    internal delegate nint WndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);
    internal delegate int MonitorEnumProc(nint hMonitor, nint hdcMonitor, RECT* lprcMonitor, nint dwData);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassExW(in WNDCLASSEXW lpwcx);

    [LibraryImport(User32, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterClassW(string lpClassName, nint hInstance);

    [LibraryImport(User32, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    [LibraryImport(User32)]
    public static partial nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateWindow(nint hWnd);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AdjustWindowRectEx(
        ref RECT lpRect,
        uint dwStyle,
        [MarshalAs(UnmanagedType.Bool)] bool bMenu,
        uint dwExStyle);

    [LibraryImport(User32, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowTextW(nint hWnd, string lpString);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PeekMessageW(
        out MSG lpMsg,
        nint hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax,
        uint wRemoveMsg);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport(User32)]
    public static partial nint DispatchMessageW(in MSG lpMsg);

    [LibraryImport(User32)]
    public static partial void PostQuitMessage(int nExitCode);

    [LibraryImport(User32)]
    public static partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFOEXW lpmi);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(nint hdc, RECT* lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessDpiAwarenessContext(nint dpiContext);

    [LibraryImport(User32)]
    public static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport(User32, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadCursorW(nint hInstance, nint lpCursorName);

    [LibraryImport(Kernel32, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport(Kernel32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryPerformanceCounter(out long lpPerformanceCount);

    [LibraryImport(Kernel32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryPerformanceFrequency(out long lpFrequency);

    public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return SetWindowLong32(hWnd, nIndex, unchecked((int)dwNewLong));
    }

    public static nint GetWindowLongPtr(nint hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex);
        return GetWindowLong32(hWnd, nIndex);
    }

    [LibraryImport(User32, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport(User32, EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial nint SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [LibraryImport(User32, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [LibraryImport(User32, EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial nint GetWindowLong32(nint hWnd, int nIndex);
}
