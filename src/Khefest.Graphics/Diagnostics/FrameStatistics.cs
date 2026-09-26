using System.Diagnostics;

namespace Khefest.Graphics.Diagnostics;

/// <summary>
/// Aggregates frame timing, FPS, and runtime memory/GC statistics.
/// </summary>
public sealed class FrameStatistics
{
    private long _frameCount;
    private double _elapsedTimeAccumulator;
    private double _currentFps;
    private double _currentFrameTimeMs;

    public long FrameNumber => _frameCount;
    public double FramesPerSecond => _currentFps;
    public double FrameTimeMilliseconds => _currentFrameTimeMs;
    public int Gen0Collections => GC.CollectionCount(0);
    public int Gen1Collections => GC.CollectionCount(1);
    public int Gen2Collections => GC.CollectionCount(2);
    public long ManagedMemoryBytes => GC.GetTotalMemory(false);

    /// <summary>
    /// Updates frame statistics with the delta time of the latest frame.
    /// </summary>
    public void Update(double deltaTimeSeconds)
    {
        _frameCount++;
        _currentFrameTimeMs = deltaTimeSeconds * 1000.0;
        _elapsedTimeAccumulator += deltaTimeSeconds;

        if (_elapsedTimeAccumulator >= 0.5) // update FPS twice per second for stability
        {
            _currentFps = 1.0 / (deltaTimeSeconds > 0.0001 ? deltaTimeSeconds : 0.0001);
            _elapsedTimeAccumulator = 0;
        }
    }
}
