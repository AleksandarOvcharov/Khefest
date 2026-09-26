using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.Texturing;

/// <summary>
/// High-level 2D offscreen render target wrapping an underlying <see cref="IGpuTexture"/>.
/// Supports dynamic resizing, optional hardware depth-stencil buffering, and automatic lifecycle management.
/// Implements <see cref="IGpuTexture"/> directly so it can be passed anywhere a GPU texture or target is expected.
/// </summary>
public sealed class RenderTarget2D : ResourceBase, IGpuTexture
{
    private readonly IGpuDevice _device;
    private readonly ResourceManager? _manager;
    private readonly GpuFormat _format;
    private readonly TextureUsage _colorUsage;
    private readonly bool _hasDepth;
    private IGpuTexture _colorTexture;
    private IGpuTexture? _depthTexture;

    public int Width => _colorTexture.Width;
    public int Height => _colorTexture.Height;
    public GpuFormat Format => _format;
    public TextureUsage Usage => _colorUsage;

    public IGpuTexture ColorTexture => _colorTexture;
    public IGpuTexture? DepthTexture => _depthTexture;
    public bool HasDepth => _hasDepth;
    public IGpuDevice Device => _device;

    /// <summary>
    /// Gets the canonical GPU texture represented by this render target for low-level backend binding.
    /// </summary>
    public IGpuTexture CanonicalTexture => _colorTexture.CanonicalTexture;

    public RenderTarget2D(
        IGpuDevice device,
        string name,
        int width,
        int height,
        bool hasDepth = false,
        GpuFormat format = GpuFormat.R8G8B8A8_UNorm,
        ResourceManager? manager = null)
        : base(name, ResourceType.Texture, (long)width * height * 4, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _manager = manager;
        _format = format;
        _hasDepth = hasDepth;
        _colorUsage = TextureUsage.RenderTarget | TextureUsage.Sampled;

        _colorTexture = _device.CreateTexture(
            $"{name}_Color",
            width,
            height,
            _format,
            _colorUsage);

        if (_hasDepth)
        {
            _depthTexture = _device.CreateTexture(
                $"{name}_Depth",
                width,
                height,
                GpuFormat.D24_UNorm_S8_UInt,
                TextureUsage.DepthStencil);
        }

        manager?.Register(this);
    }

    /// <summary>
    /// Resizes the render target if the dimensions differ from the current size.
    /// Reallocates the color texture (and depth buffer if enabled) and releases the previous textures.
    /// </summary>
    /// <param name="width">New width in pixels.</param>
    /// <param name="height">New height in pixels.</param>
    /// <returns>True if resized; false if dimensions were already identical.</returns>
    public bool Resize(int width, int height)
    {
        ThrowIfDisposed();
        if (width <= 0 || height <= 0) return false;
        if (width == Width && height == Height) return false;

        _colorTexture.Dispose();
        _depthTexture?.Dispose();

        _colorTexture = _device.CreateTexture(
            $"{Name}_Color",
            width,
            height,
            _format,
            _colorUsage);

        if (_hasDepth)
        {
            _depthTexture = _device.CreateTexture(
                $"{Name}_Depth",
                width,
                height,
                GpuFormat.D24_UNorm_S8_UInt,
                TextureUsage.DepthStencil);
        }

        return true;
    }

    /// <summary>
    /// Helper to create a <see cref="RenderPassDesc"/> targeting this render target.
    /// </summary>
    public RenderPassDesc CreatePass(Color4? clearColor = null, bool clearDepth = true)
    {
        ThrowIfDisposed();
        return new RenderPassDesc
        {
            ColorTarget = _colorTexture,
            ClearColorTarget = clearColor.HasValue,
            ClearColor = clearColor ?? Color4.Black,
            DepthTarget = _depthTexture,
            ClearDepthTarget = _hasDepth && clearDepth,
            ClearDepth = 1.0f
        };
    }

    public void SetData<T>(ReadOnlySpan<T> data, int subresource = 0, int rowPitchInBytes = 0) where T : unmanaged
    {
        ThrowIfDisposed();
        _colorTexture.SetData(data, subresource, rowPitchInBytes);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _depthTexture?.Dispose();
            _colorTexture.Dispose();
        }
    }
}
