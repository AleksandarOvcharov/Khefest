namespace Khefest.Windows.Timing;

/// <summary>
/// Snapshot of application timing information for a single frame.
/// </summary>
public readonly record struct GameTime(
    TimeSpan TotalTime,
    TimeSpan ElapsedTime,
    long FrameCount,
    float Fps)
{
    /// <summary>
    /// Elapsed frame delta time in fractional seconds.
    /// </summary>
    public float DeltaTime => (float)ElapsedTime.TotalSeconds;

    /// <summary>
    /// Elapsed frame delta time in fractional seconds (alias of <see cref="DeltaTime"/>).
    /// </summary>
    public float DeltaTimeSeconds => (float)ElapsedTime.TotalSeconds;

    /// <summary>
    /// Total accumulated running time in fractional seconds.
    /// </summary>
    public double TotalSeconds => TotalTime.TotalSeconds;
}
