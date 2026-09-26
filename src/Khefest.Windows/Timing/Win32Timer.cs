using System.Diagnostics;
using Khefest.Windows.Native;

namespace Khefest.Windows.Timing;

/// <summary>
/// High-resolution Win32 timer backed by the CPU performance counter (QPC).
/// </summary>
public sealed class Win32Timer
{
    private readonly long _frequency;
    private long _startCounter;
    private long _lastCounter;
    private long _frameCount;

    private double _fpsAccumulator;
    private int _fpsCounter;
    private float _currentFps;

    public Win32Timer()
    {
        if (!Win32Native.QueryPerformanceFrequency(out _frequency) || _frequency == 0)
        {
            _frequency = Stopwatch.Frequency;
        }

        Reset();
    }

    public void Reset()
    {
        Win32Native.QueryPerformanceCounter(out _startCounter);
        _lastCounter = _startCounter;
        _frameCount = 0;
        _fpsAccumulator = 0;
        _fpsCounter = 0;
        _currentFps = 0;
    }

    public GameTime Tick()
    {
        Win32Native.QueryPerformanceCounter(out var currentCounter);
        var elapsedTicks = currentCounter - _lastCounter;
        _lastCounter = currentCounter;

        var elapsedSeconds = (double)elapsedTicks / _frequency;
        var totalSeconds = (double)(currentCounter - _startCounter) / _frequency;

        _frameCount++;
        _fpsCounter++;
        _fpsAccumulator += elapsedSeconds;

        if (_fpsAccumulator >= 1.0)
        {
            _currentFps = (float)(_fpsCounter / _fpsAccumulator);
            _fpsCounter = 0;
            _fpsAccumulator = 0;
        }

        return new GameTime(
            TimeSpan.FromSeconds(totalSeconds),
            TimeSpan.FromSeconds(elapsedSeconds),
            _frameCount,
            _currentFps);
    }
}
