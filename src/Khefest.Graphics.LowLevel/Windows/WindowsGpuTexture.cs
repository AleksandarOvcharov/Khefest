using Khefest.Core.Resources;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native implementation of <see cref="IGpuTexture"/> backed by Direct3D 11.
/// </summary>
public sealed class WindowsGpuTexture : ResourceBase, IGpuTexture
{
    private nint _texture;
    private nint _renderTargetView;
    private nint _shaderResourceView;
    private nint _depthStencilView;
    private readonly nint _deviceContext;

    public int Width { get; }
    public int Height { get; }
    public GpuFormat Format { get; }
    public TextureUsage Usage { get; }

    public nint NativeTexture => _texture;
    public nint NativeRenderTargetView => _renderTargetView;
    public nint NativeShaderResourceView => _shaderResourceView;
    public nint NativeDepthStencilView => _depthStencilView;

    public WindowsGpuTexture(
        string name,
        nint texture,
        int width,
        int height,
        GpuFormat format,
        TextureUsage usage,
        nint renderTargetView = default,
        nint shaderResourceView = default,
        ResourceManager? manager = null,
        nint depthStencilView = default,
        nint deviceContext = default)
        : base(name, ResourceType.Texture, (long)width * height * 4, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _texture = texture;
        Width = width;
        Height = height;
        Format = format;
        Usage = usage;
        _renderTargetView = renderTargetView;
        _shaderResourceView = shaderResourceView;
        _depthStencilView = depthStencilView;
        _deviceContext = deviceContext;
        manager?.Register(this);
    }

    public unsafe void SetData<T>(ReadOnlySpan<T> data, int subresource = 0, int rowPitchInBytes = 0) where T : unmanaged
    {
        ThrowIfDisposed();
        if (data.IsEmpty || _texture == nint.Zero || _deviceContext == nint.Zero) return;

        if (rowPitchInBytes <= 0)
        {
            rowPitchInBytes = Width * sizeof(T);
        }

        fixed (T* pSrc = data)
        {
            var contextVTable = *(void***)_deviceContext;
            var updateSubresource = (delegate* unmanaged[Stdcall]<nint, nint, uint, void*, void*, uint, uint, void>)contextVTable[48];
            updateSubresource(_deviceContext, _texture, (uint)subresource, null, pSrc, (uint)rowPitchInBytes, 0);
        }
    }

    internal void UpdateViews(nint renderTargetView, nint shaderResourceView, nint depthStencilView = default)
    {
        _renderTargetView = renderTargetView;
        _shaderResourceView = shaderResourceView;
        _depthStencilView = depthStencilView;
    }

    protected override void Dispose(bool disposing)
    {
        if (_depthStencilView != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _depthStencilView);
        }

        if (_shaderResourceView != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _shaderResourceView);
        }

        if (_renderTargetView != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _renderTargetView);
        }

        if (_texture != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _texture);
        }
    }
}
