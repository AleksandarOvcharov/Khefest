namespace Khefest.Core.Profiling;

/// <summary>
/// Represents an immutable snapshot of a single profiling sample node within a hierarchical frame profile.
/// </summary>
public sealed class ProfileSample
{
    public string Name { get; }
    public double ElapsedMilliseconds { get; }
    public long CallCount { get; }
    public IReadOnlyList<ProfileSample> Children { get; }

    public ProfileSample(string name, double elapsedMs, long callCount, IReadOnlyList<ProfileSample> children)
    {
        Name = name;
        ElapsedMilliseconds = elapsedMs;
        CallCount = callCount;
        Children = children;
    }

    public override string ToString() => $"{Name}: {ElapsedMilliseconds:F3} ms ({CallCount} calls)";
}

/// <summary>
/// Snapshot of an entire frame's profiling results.
/// </summary>
public sealed class ProfileFrame
{
    public long FrameIndex { get; }
    public double TotalFrameTimeMilliseconds { get; }
    public IReadOnlyList<ProfileSample> RootSamples { get; }

    public ProfileFrame(long frameIndex, double totalFrameTimeMs, IReadOnlyList<ProfileSample> rootSamples)
    {
        FrameIndex = frameIndex;
        TotalFrameTimeMilliseconds = totalFrameTimeMs;
        RootSamples = rootSamples;
    }
}
