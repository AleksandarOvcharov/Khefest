namespace Khefest.Windowing;

/// <summary>
/// Information regarding a connected display monitor.
/// </summary>
public interface IDisplayMonitor
{
    string DeviceName { get; }
    int X { get; }
    int Y { get; }
    int Width { get; }
    int Height { get; }
    int WorkAreaX { get; }
    int WorkAreaY { get; }
    int WorkAreaWidth { get; }
    int WorkAreaHeight { get; }
    int RefreshRate { get; }
    bool IsPrimary { get; }
}

/// <summary>
/// Service for querying connected display monitors.
/// </summary>
public interface IDisplayService
{
    IReadOnlyList<IDisplayMonitor> GetMonitors();
    IDisplayMonitor? GetPrimaryMonitor();
}
