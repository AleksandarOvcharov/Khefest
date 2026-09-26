using System.Runtime.InteropServices;
using Khefest.Windowing;
using Khefest.Windows.Native;

namespace Khefest.Windows.Display;

/// <summary>
/// Win32 implementation of a display monitor.
/// </summary>
public sealed class Win32DisplayMonitor : IDisplayMonitor
{
    public string DeviceName { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int WorkAreaX { get; init; }
    public int WorkAreaY { get; init; }
    public int WorkAreaWidth { get; init; }
    public int WorkAreaHeight { get; init; }
    public int RefreshRate { get; init; } = 60;
    public bool IsPrimary { get; init; }

    public override string ToString() =>
        $"{DeviceName} ({Width}x{Height} @ {X},{Y}{(IsPrimary ? " [Primary]" : "")})";
}

/// <summary>
/// Service for querying connected Windows display monitors via Win32.
/// </summary>
public sealed class Win32DisplayService : IDisplayService
{
    public unsafe IReadOnlyList<IDisplayMonitor> GetMonitors()
    {
        var monitors = new List<IDisplayMonitor>();

        Win32Native.MonitorEnumProc callback = (nint hMonitor, nint _, RECT* _, nint _) =>
        {
            var info = new MONITORINFOEXW { CbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>() };
            if (Win32Native.GetMonitorInfoW(hMonitor, ref info))
            {
                monitors.Add(new Win32DisplayMonitor
                {
                    DeviceName = info.SzDevice,
                    X = info.RcMonitor.Left,
                    Y = info.RcMonitor.Top,
                    Width = info.RcMonitor.Width,
                    Height = info.RcMonitor.Height,
                    WorkAreaX = info.RcWork.Left,
                    WorkAreaY = info.RcWork.Top,
                    WorkAreaWidth = info.RcWork.Width,
                    WorkAreaHeight = info.RcWork.Height,
                    IsPrimary = (info.Flags & Win32Constants.MONITORINFOF_PRIMARY) != 0
                });
            }
            return 1; // Continue enumeration
        };

        Win32Native.EnumDisplayMonitors(nint.Zero, null, callback, nint.Zero);
        GC.KeepAlive(callback);

        return monitors;
    }

    public IDisplayMonitor? GetPrimaryMonitor()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }
}
