using System.Runtime.InteropServices;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.Texturing;

/// <summary>
/// High-level 2D texture representing an image on the GPU.
/// Provides convenient creation, pixel uploading, and high-level drawing integration.
/// </summary>
public sealed class Texture2D : ResourceBase
{
    private readonly IGpuTexture _gpuTexture;

    public int Width => _gpuTexture.Width;
    public int Height => _gpuTexture.Height;
    public GpuFormat Format => _gpuTexture.Format;
    public IGpuTexture GpuTexture => _gpuTexture;

    public Texture2D(string name, IGpuTexture gpuTexture, ResourceManager? manager = null)
        : base(name, ResourceType.Texture, (long)gpuTexture.Width * gpuTexture.Height * 4, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _gpuTexture = gpuTexture ?? throw new ArgumentNullException(nameof(gpuTexture));
        manager?.Register(this);
    }

    /// <summary>
    /// Creates a 1x1 or custom sized solid color texture.
    /// </summary>
    public static Texture2D CreateSolid(
        IGpuDevice device,
        string name,
        int width,
        int height,
        Color4 color,
        ResourceManager? manager = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        var r = (byte)(Math.Clamp(color.R, 0f, 1f) * 255f);
        var g = (byte)(Math.Clamp(color.G, 0f, 1f) * 255f);
        var b = (byte)(Math.Clamp(color.B, 0f, 1f) * 255f);
        var a = (byte)(Math.Clamp(color.A, 0f, 1f) * 255f);

        var pixel = (uint)(r | (g << 8) | (b << 16) | (a << 24));
        var totalPixels = width * height;
        var pixels = new uint[totalPixels];
        Array.Fill(pixels, pixel);

        return FromRgba(device, name, width, height, MemoryMarshal.AsBytes<uint>(pixels), manager);
    }

    /// <summary>
    /// Creates a Texture2D from raw 32-bit RGBA pixel bytes.
    /// </summary>
    public static Texture2D FromRgba(
        IGpuDevice device,
        string name,
        int width,
        int height,
        ReadOnlySpan<byte> rgbaPixels,
        ResourceManager? manager = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        var gpuTex = device.CreateTexture(
            name,
            width,
            height,
            GpuFormat.R8G8B8A8_UNorm,
            TextureUsage.Sampled);

        gpuTex.SetData(rgbaPixels, 0, width * 4);

        return new Texture2D(name, gpuTex, manager);
    }

    /// <summary>
    /// Loads a texture from an image file (PNG, JPG, BMP) using the native Windows Imaging Component.
    /// </summary>
    public static Texture2D FromFile(
        IGpuDevice device,
        string filePath,
        ResourceManager? manager = null)
    {
        var decoded = WicImageDecoder.DecodeFromFile(filePath);
        var name = Path.GetFileNameWithoutExtension(filePath);
        return FromRgba(device, name, decoded.Width, decoded.Height, decoded.Pixels, manager);
    }

    /// <summary>
    /// Updates texture subresource data.
    /// </summary>
    public void SetData<T>(ReadOnlySpan<T> data, int subresource = 0, int rowPitchInBytes = 0) where T : unmanaged
    {
        ThrowIfDisposed();
        _gpuTexture.SetData(data, subresource, rowPitchInBytes);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gpuTexture.Dispose();
        }
    }
}
