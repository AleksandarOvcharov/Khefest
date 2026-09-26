using System.Diagnostics;

namespace Khefest.Core.Profiling;

/// <summary>
/// Hierarchical CPU performance profiler measuring execution scopes using high-resolution performance counters.
/// </summary>
public static class Profiler
{
    private sealed class Node
    {
        public string Name { get; }
        public Node? Parent { get; }
        public List<Node> Children { get; } = new();
        public long StartTimestamp { get; set; }
        public long TotalTicks { get; set; }
        public long CallCount { get; set; }

        public Node(string name, Node? parent)
        {
            Name = name;
            Parent = parent;
        }

        public void Reset()
        {
            TotalTicks = 0;
            CallCount = 0;
            foreach (var child in Children)
            {
                child.Reset();
            }
        }

        public ProfileSample ToSnapshot()
        {
            double elapsedMs = (double)TotalTicks / Stopwatch.Frequency * 1000.0;
            var childSnapshots = new List<ProfileSample>(Children.Count);
            foreach (var child in Children)
            {
                childSnapshots.Add(child.ToSnapshot());
            }
            return new ProfileSample(Name, elapsedMs, CallCount, childSnapshots);
        }
    }

    private static readonly object SyncLock = new();
    private static readonly List<Node> RootNodes = new();
    private static Node? _currentNode;
    private static long _frameStartIndex;
    private static long _frameStartTimestamp;
    private static ProfileFrame? _lastCompletedFrame;

    private const int HistoryCapacity = 120;
    private static readonly double[] FrameTimeHistory = new double[HistoryCapacity];
    private static int _historyIndex;
    private static int _historyCount;

    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the most recently completed frame profile.
    /// </summary>
    public static ProfileFrame? LastCompletedFrame
    {
        get
        {
            lock (SyncLock)
            {
                return _lastCompletedFrame;
            }
        }
    }

    /// <summary>
    /// Begins a profiled scope using a zero-allocation disposable ref struct.
    /// Usage: using (Profiler.Scope("PhysicsUpdate")) { ... }
    /// </summary>
    public static ProfileScope Scope(string name) => new(name);

    /// <summary>
    /// Starts a named sample node in the current hierarchy.
    /// </summary>
    public static void BeginSample(string name)
    {
        if (!IsEnabled) return;

        lock (SyncLock)
        {
            Node targetNode;
            if (_currentNode == null)
            {
                targetNode = RootNodes.Find(n => n.Name == name)!;
                if (targetNode == null)
                {
                    targetNode = new Node(name, null);
                    RootNodes.Add(targetNode);
                }
            }
            else
            {
                targetNode = _currentNode.Children.Find(n => n.Name == name)!;
                if (targetNode == null)
                {
                    targetNode = new Node(name, _currentNode);
                    _currentNode.Children.Add(targetNode);
                }
            }

            targetNode.StartTimestamp = Stopwatch.GetTimestamp();
            targetNode.CallCount++;
            _currentNode = targetNode;
        }
    }

    /// <summary>
    /// Ends the innermost active sample node.
    /// </summary>
    public static void EndSample()
    {
        if (!IsEnabled) return;

        lock (SyncLock)
        {
            if (_currentNode == null) return;

            long elapsed = Stopwatch.GetTimestamp() - _currentNode.StartTimestamp;
            _currentNode.TotalTicks += elapsed;
            _currentNode = _currentNode.Parent;
        }
    }

    /// <summary>
    /// Begins profiling a new frame.
    /// </summary>
    public static void BeginFrame()
    {
        if (!IsEnabled) return;

        lock (SyncLock)
        {
            _currentNode = null;
            foreach (var node in RootNodes)
            {
                node.Reset();
            }
            _frameStartTimestamp = Stopwatch.GetTimestamp();
        }
    }

    /// <summary>
    /// Ends profiling of the current frame and returns a snapshot of hierarchical timing results.
    /// </summary>
    public static ProfileFrame? EndFrame()
    {
        if (!IsEnabled) return null;

        lock (SyncLock)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - _frameStartTimestamp;
            double totalFrameMs = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;

            var snapshots = new List<ProfileSample>(RootNodes.Count);
            foreach (var root in RootNodes)
            {
                snapshots.Add(root.ToSnapshot());
            }

            var frame = new ProfileFrame(_frameStartIndex++, totalFrameMs, snapshots);
            _lastCompletedFrame = frame;

            // Update rolling history
            FrameTimeHistory[_historyIndex] = totalFrameMs;
            _historyIndex = (_historyIndex + 1) % HistoryCapacity;
            if (_historyCount < HistoryCapacity)
            {
                _historyCount++;
            }

            return frame;
        }
    }

    /// <summary>
    /// Gets aggregated frame time statistics from recent frame history.
    /// </summary>
    public static (double AverageMs, double MinMs, double MaxMs) GetFrameStatistics()
    {
        lock (SyncLock)
        {
            if (_historyCount == 0) return (0, 0, 0);

            double sum = 0;
            double min = double.MaxValue;
            double max = double.MinValue;

            for (int i = 0; i < _historyCount; i++)
            {
                double val = FrameTimeHistory[i];
                sum += val;
                if (val < min) min = val;
                if (val > max) max = val;
            }

            return (sum / _historyCount, min, max);
        }
    }

    /// <summary>
    /// Resets all profiler tree nodes and history.
    /// </summary>
    public static void Reset()
    {
        lock (SyncLock)
        {
            RootNodes.Clear();
            _currentNode = null;
            _historyIndex = 0;
            _historyCount = 0;
            _lastCompletedFrame = null;
        }
    }
}
