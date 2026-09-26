using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.Memory;

/// <summary>
/// A disposable lease for a pooled GPU buffer. Disposing returns the buffer to the pool.
/// </summary>
public readonly struct GpuBufferLease : IDisposable
{
    private readonly GpuBufferPool _pool;
    public IGpuBuffer Buffer { get; }

    internal GpuBufferLease(GpuBufferPool pool, IGpuBuffer buffer)
    {
        _pool = pool;
        Buffer = buffer;
    }

    public void Dispose()
    {
        _pool.Return(Buffer);
    }
}

/// <summary>
/// High-performance GPU buffer pool that recycles buffers by size category and usage,
/// avoiding expensive per-frame GPU allocation and driver overhead.
/// </summary>
public sealed class GpuBufferPool : ResourceBase
{
    private readonly struct PoolKey : IEquatable<PoolKey>
    {
        public readonly int BucketSize;
        public readonly BufferUsage Usage;

        public PoolKey(int bucketSize, BufferUsage usage)
        {
            BucketSize = bucketSize;
            Usage = usage;
        }

        public bool Equals(PoolKey other) => BucketSize == other.BucketSize && Usage == other.Usage;
        public override bool Equals(object? obj) => obj is PoolKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BucketSize, (int)Usage);
    }

    private readonly IGpuDevice _device;
    private readonly Dictionary<PoolKey, Stack<IGpuBuffer>> _available = new();
    private readonly HashSet<IGpuBuffer> _inUse = new();
    private readonly Lock _lock = new();
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Resource, "GpuBufferPool");
    private long _totalPooledBytes;

    public long TotalPooledBytes
    {
        get
        {
            lock (_lock) return _totalPooledBytes;
        }
    }

    public int ActiveLeaseCount
    {
        get
        {
            lock (_lock) return _inUse.Count;
        }
    }

    public GpuBufferPool(IGpuDevice device, ResourceManager? manager = null)
        : base("GpuBufferPool", ResourceType.Custom, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        manager?.Register(this);
    }

    /// <summary>
    /// Rents a GPU buffer of at least the specified size and usage.
    /// </summary>
    public GpuBufferLease Rent(int minimumSize, BufferUsage usage)
    {
        ThrowIfDisposed();
        int bucketSize = NextPowerOfTwo(Math.Max(256, minimumSize));
        var key = new PoolKey(bucketSize, usage);

        IGpuBuffer buffer;
        lock (_lock)
        {
            if (_available.TryGetValue(key, out var stack) && stack.TryPop(out var pooledBuffer))
            {
                buffer = pooledBuffer;
            }
            else
            {
                buffer = _device.CreateBuffer($"PooledBuffer_{usage}_{bucketSize}", bucketSize, usage);
                _totalPooledBytes += bucketSize;
            }

            _inUse.Add(buffer);
        }

        return new GpuBufferLease(this, buffer);
    }

    internal void Return(IGpuBuffer buffer)
    {
        if (buffer == null || buffer.IsDisposed) return;

        lock (_lock)
        {
            if (_inUse.Remove(buffer))
            {
                var key = new PoolKey((int)buffer.SizeInBytes, buffer.Usage);
                if (!_available.TryGetValue(key, out var stack))
                {
                    stack = new Stack<IGpuBuffer>();
                    _available[key] = stack;
                }
                stack.Push(buffer);
            }
        }
    }

    /// <summary>
    /// Flushes all idle buffers currently in the pool, freeing GPU memory.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            foreach (var stack in _available.Values)
            {
                while (stack.TryPop(out var buffer))
                {
                    _totalPooledBytes -= buffer.SizeInBytes;
                    buffer.Dispose();
                }
            }
            _available.Clear();
        }
    }

    protected override void Dispose(bool disposing)
    {
        lock (_lock)
        {
            foreach (var stack in _available.Values)
            {
                while (stack.TryPop(out var buffer))
                {
                    buffer.Dispose();
                }
            }
            _available.Clear();

            foreach (var buffer in _inUse)
            {
                buffer.Dispose();
            }
            _inUse.Clear();
            _totalPooledBytes = 0;
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        int power = 1;
        while (power < value)
        {
            power <<= 1;
        }
        return power;
    }
}
