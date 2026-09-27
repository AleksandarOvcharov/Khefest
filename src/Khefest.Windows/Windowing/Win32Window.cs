using System.Runtime.InteropServices;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Input;
using Khefest.Windowing;
using Khefest.Windows.Native;

namespace Khefest.Windows.Windowing;

/// <summary>
/// Native Win32 window implementation backed by a Win32 message loop and raw HWND.
/// </summary>
public sealed class Win32Window : IWindow
{
    private const string WindowClassName = "KhefestWindowClass";
    private static bool _classRegistered;
    private static readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Platform, "Win32Window");

    private nint _hwnd;
    private readonly Win32Native.WndProcDelegate _wndProcDelegate;
    private GCHandle _wndProcHandle;
    private readonly IInputService? _input;

    private string _title;
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private int _clientWidth;
    private int _clientHeight;
    private bool _isVisible;
    private bool _isFocused;
    private bool _shouldClose;
    private WindowMode _mode;
    private RECT _savedWindowRect;
    private bool _isDisposed;

    public nint Handle => _hwnd;
    public string Title
    {
        get => _title;
        set
        {
            ThrowIfDisposed();
            _title = value ?? string.Empty;
            Win32Native.SetWindowTextW(_hwnd, _title);
        }
    }

    public int X => _x;
    public int Y => _y;
    public int Width => _width;
    public int Height => _height;
    public int ClientWidth => _clientWidth;
    public int ClientHeight => _clientHeight;
    public bool IsVisible => _isVisible;
    public bool IsFocused => _isFocused;
    public bool ShouldClose => _shouldClose;
    public WindowMode Mode => _mode;

    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action<bool>? FocusChanged;
    public event Action? CloseRequested;

    public Win32Window(WindowConfig config, IInputService? input = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _input = input;
        _title = config.Title;
        _width = config.Width;
        _height = config.Height;
        _mode = config.Mode;

        // Keep delegate alive and pinned to avoid garbage collection of native callback
        _wndProcDelegate = WndProc;
        _wndProcHandle = GCHandle.Alloc(_wndProcDelegate);

        RegisterWindowClass();
        CreateWindow(config);
    }

    private void RegisterWindowClass()
    {
        if (_classRegistered) return;

        var hInstance = Win32Native.GetModuleHandleW(null);
        var defWindowProcPtr = NativeLibrary.GetExport(NativeLibrary.Load("user32.dll"), "DefWindowProcW");

        var wc = new WNDCLASSEXW
        {
            CbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            Style = Win32Constants.CS_HREDRAW | Win32Constants.CS_VREDRAW | Win32Constants.CS_OWNDC,
            LpfnWndProc = defWindowProcPtr,
            HInstance = hInstance,
            HCursor = Win32Native.LoadCursorW(nint.Zero, (nint)32512), // IDC_ARROW
            LpszClassName = WindowClassName
        };

        var atom = Win32Native.RegisterClassExW(in wc);
        if (atom == 0)
        {
            var err = Marshal.GetLastWin32Error();
            // 1410 = ERROR_CLASS_ALREADY_EXISTS, which is safe to ignore
            if (err != 1410)
            {
                throw new KhefestPlatformException(
                    KhefestErrorCode.PlatformNativeInteropError,
                    "Failed to register Win32 window class",
                    err);
            }
        }
        else
        {
            _classRegistered = true;
        }
    }

    private void CreateWindow(WindowConfig config)
    {
        var hInstance = Win32Native.GetModuleHandleW(null);
        uint dwStyle = Win32Constants.WS_OVERLAPPEDWINDOW;
        uint dwExStyle = Win32Constants.WS_EX_APPWINDOW;

        if (!config.Resizable)
        {
            dwStyle &= ~(Win32Constants.WS_THICKFRAME | Win32Constants.WS_MAXIMIZEBOX);
        }

        var rect = new RECT { Left = 0, Top = 0, Right = _width, Bottom = _height };
        Win32Native.AdjustWindowRectEx(ref rect, dwStyle, false, dwExStyle);

        var windowWidth = rect.Width;
        var windowHeight = rect.Height;

        _hwnd = Win32Native.CreateWindowExW(
            dwExStyle,
            WindowClassName,
            _title,
            dwStyle,
            Win32Constants.CW_USEDEFAULT,
            Win32Constants.CW_USEDEFAULT,
            windowWidth,
            windowHeight,
            nint.Zero,
            nint.Zero,
            hInstance,
            nint.Zero);

        if (_hwnd == nint.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            throw new KhefestPlatformException(
                KhefestErrorCode.PlatformWindowCreationFailed,
                $"Failed to create Win32 window '{_title}'",
                err);
        }

        // Subclass window procedure for this instance
        var wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        Win32Native.SetWindowLongPtr(_hwnd, Win32Constants.GWLP_WNDPROC, wndProcPtr);

        UpdateBounds();

        if (_mode != WindowMode.Windowed)
        {
            SetMode(_mode);
        }

        _logger.Info($"Win32 window created: HWND=0x{_hwnd:X}, ClientSize={_clientWidth}x{_clientHeight}");
    }

    private void UpdateBounds()
    {
        if (Win32Native.GetWindowRect(_hwnd, out var wr))
        {
            _x = wr.Left;
            _y = wr.Top;
            _width = wr.Width;
            _height = wr.Height;
        }

        if (Win32Native.GetClientRect(_hwnd, out var cr))
        {
            _clientWidth = cr.Width;
            _clientHeight = cr.Height;
        }
    }

    public void Show()
    {
        ThrowIfDisposed();
        Win32Native.ShowWindow(_hwnd, Win32Constants.SW_SHOW);
        Win32Native.UpdateWindow(_hwnd);
        _isVisible = true;
    }

    public void Hide()
    {
        ThrowIfDisposed();
        Win32Native.ShowWindow(_hwnd, Win32Constants.SW_HIDE);
        _isVisible = false;
    }

    public void SetSize(int width, int height)
    {
        ThrowIfDisposed();
        var rect = new RECT { Left = 0, Top = 0, Right = width, Bottom = height };
        uint dwStyle = (uint)Win32Native.GetWindowLongPtr(_hwnd, -16); // GWL_STYLE
        Win32Native.AdjustWindowRectEx(ref rect, dwStyle, false, Win32Constants.WS_EX_APPWINDOW);

        Win32Native.SetWindowPos(
            _hwnd,
            nint.Zero,
            0,
            0,
            rect.Width,
            rect.Height,
            Win32Constants.SWP_NOMOVE | Win32Constants.SWP_NOZORDER | Win32Constants.SWP_FRAMECHANGED);

        UpdateBounds();
    }

    public void SetPosition(int x, int y)
    {
        ThrowIfDisposed();
        Win32Native.SetWindowPos(
            _hwnd,
            nint.Zero,
            x,
            y,
            0,
            0,
            Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOZORDER);
        UpdateBounds();
    }

    public void SetMode(WindowMode mode)
    {
        ThrowIfDisposed();
        if (_mode == mode) return;

        if (mode == WindowMode.BorderlessFullscreen || mode == WindowMode.ExclusiveFullscreen)
        {
            // Save current position and style for restoration
            Win32Native.GetWindowRect(_hwnd, out _savedWindowRect);

            var hMonitor = Win32Native.MonitorFromWindow(_hwnd, Win32Constants.MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFOEXW { CbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>() };
            Win32Native.GetMonitorInfoW(hMonitor, ref mi);

            var style = Win32Constants.WS_POPUP | Win32Constants.WS_VISIBLE;
            Win32Native.SetWindowLongPtr(_hwnd, -16, (nint)style);

            Win32Native.SetWindowPos(
                _hwnd,
                nint.Zero,
                mi.RcMonitor.Left,
                mi.RcMonitor.Top,
                mi.RcMonitor.Width,
                mi.RcMonitor.Height,
                Win32Constants.SWP_NOZORDER | Win32Constants.SWP_FRAMECHANGED | Win32Constants.SWP_SHOWWINDOW);
        }
        else // Windowed
        {
            var style = Win32Constants.WS_OVERLAPPEDWINDOW | Win32Constants.WS_VISIBLE;
            Win32Native.SetWindowLongPtr(_hwnd, -16, (nint)style);

            var x = _savedWindowRect.Left;
            var y = _savedWindowRect.Top;
            var w = _savedWindowRect.Width > 0 ? _savedWindowRect.Width : 1280;
            var h = _savedWindowRect.Height > 0 ? _savedWindowRect.Height : 720;

            Win32Native.SetWindowPos(
                _hwnd,
                nint.Zero,
                x,
                y,
                w,
                h,
                Win32Constants.SWP_NOZORDER | Win32Constants.SWP_FRAMECHANGED | Win32Constants.SWP_SHOWWINDOW);
        }

        _mode = mode;
        UpdateBounds();
        _logger.Info($"Window mode changed to: {mode} (Size: {_clientWidth}x{_clientHeight})");
    }

    public void RequestClose()
    {
        _shouldClose = true;
        CloseRequested?.Invoke();
    }

    public void PollEvents()
    {
        ThrowIfDisposed();

        while (Win32Native.PeekMessageW(out var msg, nint.Zero, 0, 0, Win32Constants.PM_REMOVE))
        {
            if (msg.Message == Win32Constants.WM_QUIT)
            {
                _shouldClose = true;
                break;
            }

            Win32Native.TranslateMessage(in msg);
            Win32Native.DispatchMessageW(in msg);
        }
    }

    private nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32Constants.WM_SIZE:
            {
                var newClientW = unchecked((short)(lParam & 0xFFFF));
                var newClientH = unchecked((short)((lParam >> 16) & 0xFFFF));
                _clientWidth = newClientW;
                _clientHeight = newClientH;
                UpdateBounds();
                Resized?.Invoke(newClientW, newClientH);
                return 0;
            }

            case Win32Constants.WM_MOVE:
            {
                var newX = unchecked((short)(lParam & 0xFFFF));
                var newY = unchecked((short)((lParam >> 16) & 0xFFFF));
                _x = newX;
                _y = newY;
                Moved?.Invoke(newX, newY);
                return 0;
            }

            case Win32Constants.WM_SETFOCUS:
                _isFocused = true;
                FocusChanged?.Invoke(true);
                return 0;

            case Win32Constants.WM_KILLFOCUS:
                _isFocused = false;
                FocusChanged?.Invoke(false);
                return 0;

            case Win32Constants.WM_CLOSE:
                RequestClose();
                return 0;

            case Win32Constants.WM_DESTROY:
                _hwnd = nint.Zero;
                _shouldClose = true;
                return 0;

            // Keyboard input routing
            case Win32Constants.WM_KEYDOWN:
            case Win32Constants.WM_SYSKEYDOWN:
            {
                var key = Win32KeyMapper.MapVirtualKey(wParam);
                if (key != Key.None)
                {
                    _input?.OnKeyDown(key);
                }
                break;
            }

            case Win32Constants.WM_KEYUP:
            case Win32Constants.WM_SYSKEYUP:
            {
                var key = Win32KeyMapper.MapVirtualKey(wParam);
                if (key != Key.None)
                {
                    _input?.OnKeyUp(key);
                }
                break;
            }

            case Win32Constants.WM_CHAR:
            {
                var c = (char)wParam;
                _input?.OnChar(c);
                return 0;
            }

            // Mouse input routing
            case Win32Constants.WM_MOUSEMOVE:
            {
                var mouseX = unchecked((short)(lParam & 0xFFFF));
                var mouseY = unchecked((short)((lParam >> 16) & 0xFFFF));
                _input?.OnMouseMove(mouseX, mouseY);
                return 0;
            }

            case Win32Constants.WM_LBUTTONDOWN:
                _input?.OnMouseDown(MouseButton.Left);
                return 0;

            case Win32Constants.WM_LBUTTONUP:
                _input?.OnMouseUp(MouseButton.Left);
                return 0;

            case Win32Constants.WM_RBUTTONDOWN:
                _input?.OnMouseDown(MouseButton.Right);
                return 0;

            case Win32Constants.WM_RBUTTONUP:
                _input?.OnMouseUp(MouseButton.Right);
                return 0;

            case Win32Constants.WM_MBUTTONDOWN:
                _input?.OnMouseDown(MouseButton.Middle);
                return 0;

            case Win32Constants.WM_MBUTTONUP:
                _input?.OnMouseUp(MouseButton.Middle);
                return 0;

            case Win32Constants.WM_XBUTTONDOWN:
            {
                var btn = (wParam >> 16) == 1 ? MouseButton.Button4 : MouseButton.Button5;
                _input?.OnMouseDown(btn);
                return 1;
            }

            case Win32Constants.WM_XBUTTONUP:
            {
                var btn = (wParam >> 16) == 1 ? MouseButton.Button4 : MouseButton.Button5;
                _input?.OnMouseUp(btn);
                return 1;
            }

            case Win32Constants.WM_MOUSEWHEEL:
            {
                var delta = unchecked((short)((wParam >> 16) & 0xFFFF)) / 120.0f;
                _input?.OnMouseScroll(delta);
                return 0;
            }
        }

        return Win32Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_hwnd != nint.Zero)
        {
            Win32Native.DestroyWindow(_hwnd);
            _hwnd = nint.Zero;
        }

        if (_wndProcHandle.IsAllocated)
        {
            _wndProcHandle.Free();
        }

        _logger.Info("Win32 window disposed.");
    }
}
