using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.Memory;

/// <summary>
/// A disposable lease for a pooled render target or depth-stencil texture.
/// Disposing returns the texture to the pool for reuse.
/// </summary>
public readonly struct RenderTargetLease : IDisposable
{
    private readonly RenderTargetPool _pool;
    public IGpuTexture Texture { get; }

    internal RenderTargetLease(RenderTargetPool pool, IGpuTexture texture)
    {
        _pool = pool;
        Texture = texture;
    }

    public void Dispose()
    {
        _pool.Return(Texture);
    }
}

/// <summary>
/// High-performance texture pool for scratch render targets and depth-stencil buffers.
/// </summary>
public sealed class RenderTargetPool : ResourceBase
{
    private readonly struct TextureKey : IEquatable<TextureKey>
    {
        public readonly int Width;
        public readonly int Height;
        public readonly GpuFormat Format;
        public readonly TextureUsage Usage;

        public TextureKey(int width, int height, GpuFormat format, TextureUsage usage)
        {
            Width = width;
            Height = height;
            Format = format;
            Usage = usage;
        }

        public bool Equals(TextureKey other) =>
            Width == other.Width && Height == other.Height && Format == other.Format && Usage == other.Usage;

        public override bool Equals(object? obj) => obj is TextureKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height, (int)Format, (int)Usage);
    }

    private readonly IGpuDevice _device;
    private readonly Dictionary<TextureKey, Stack<IGpuTexture>> _available = new();
    private readonly HashSet<IGpuTexture> _inUse = new();
    private readonly Lock _lock = new();
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Resource, "RenderTargetPool");
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

    public RenderTargetPool(IGpuDevice device, ResourceManager? manager = null)
        : base("RenderTargetPool", ResourceType.Custom, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        manager?.Register(this);
    }

    /// <summary>
    /// Rents a color render target texture matching the specified dimensions and format.
    /// </summary>
    public RenderTargetLease RentRenderTarget(int width, int height, GpuFormat format = GpuFormat.R8G8B8A8_UNorm)
    {
        return Rent(width, height, format, TextureUsage.RenderTarget | TextureUsage.Sampled);
    }

    /// <summary>
    /// Rents a depth-stencil texture matching the specified dimensions and format.
    /// </summary>
    public RenderTargetLease RentDepthTarget(int width, int height, GpuFormat format = GpuFormat.D24_UNorm_S8_UInt)
    {
        return Rent(width, height, format, TextureUsage.DepthStencil);
    }

    /// <summary>
    /// Rents a texture with the specified dimensions, format, and usage flags.
    /// </summary>
    public RenderTargetLease Rent(int width, int height, GpuFormat format, TextureUsage usage)
    {
        ThrowIfDisposed();
        var key = new TextureKey(width, height, format, usage);

        IGpuTexture texture;
        lock (_lock)
        {
            if (_available.TryGetValue(key, out var stack) && stack.TryPop(out var pooledTex))
            {
                texture = pooledTex;
            }
            else
            {
                texture = _device.CreateTexture($"PooledRT_{width}x{height}_{format}", width, height, format, usage);
                _totalPooledBytes += (long)width * height * 4;
            }

            _inUse.Add(texture);
        }

        return new RenderTargetLease(this, texture);
    }

    internal void Return(IGpuTexture texture)
    {
        if (texture == null || texture.IsDisposed) return;

        lock (_lock)
        {
            if (_inUse.Remove(texture))
            {
                var key = new TextureKey(texture.Width, texture.Height, texture.Format, texture.Usage);
                if (!_available.TryGetValue(key, out var stack))
                {
                    stack = new Stack<IGpuTexture>();
                    _available[key] = stack;
                }
                stack.Push(texture);
            }
        }
    }

    /// <summary>
    /// Flushes all idle pooled textures, releasing underlying GPU memory.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            foreach (var stack in _available.Values)
            {
                while (stack.TryPop(out var tex))
                {
                    _totalPooledBytes -= (long)tex.Width * tex.Height * 4;
                    tex.Dispose();
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
                while (stack.TryPop(out var tex))
                {
                    tex.Dispose();
                }
            }
            _available.Clear();

            foreach (var tex in _inUse)
            {
                tex.Dispose();
            }
            _inUse.Clear();
            _totalPooledBytes = 0;
        }
    }
}
