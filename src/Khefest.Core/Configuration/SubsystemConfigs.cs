using Khefest.Core.Logging;

namespace Khefest.Core.Configuration;

/// <summary>
/// Configuration for the Win32 window and display settings.
/// </summary>
public sealed record WindowConfig
{
    public string Title { get; init; } = "Khefest Application";
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public WindowMode Mode { get; init; } = WindowMode.Windowed;
    public bool Resizable { get; init; } = true;
    public bool VSync { get; init; } = true;
    public int TargetFps { get; init; } = 60;
}

/// <summary>
/// Configuration for graphics device initialization and validation layers.
/// </summary>
public sealed record GraphicsConfig
{
    public bool EnableValidationLayers { get; init; } = true;
    public bool PreferHighPerformanceAdapter { get; init; } = true;
    public int PreferredAdapterIndex { get; init; } = 0;
    public bool EnableMultithreadedSubmission { get; init; } = false;
}

/// <summary>
/// Configuration for GPU and host memory management.
/// Governs automatic vs manual resource lifetime management as specified in the assignment.
/// </summary>
public sealed record MemoryConfig
{
    /// <summary>
    /// When true, Khefest automatically tracks and releases GPU resource handles.
    /// When false, the developer takes explicit control over resource lifetimes.
    /// </summary>
    public bool AutoMemManagement { get; init; } = true;

    /// <summary>
    /// Enables capture of allocation call stacks to pinpoint leaked resources upon shutdown.
    /// </summary>
    public bool EnableLeakTracking { get; init; } = true;

    /// <summary>
    /// Optional limit on total tracked GPU memory in bytes (0 for unlimited).
    /// </summary>
    public long GpuMemoryBudgetBytes { get; init; } = 0;
}

/// <summary>
/// Configuration for framework logging and diagnostic sinks.
/// </summary>
public sealed record LoggingConfig
{
    public LogLevel MinimumLevel { get; init; } = LogLevel.Info;
    public bool EnableConsoleSink { get; init; } = true;
    public bool EnableMemorySink { get; init; } = true;
    public int MemorySinkCapacity { get; init; } = 1000;
}
