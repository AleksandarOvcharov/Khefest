using Khefest.Core.Errors;
using Khefest.Core.Resources;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native swap chain implementation backed by DXGI and Direct3D 11.
/// Manages double-buffering, presentation, and back-buffer resizing.
/// </summary>
public sealed unsafe class WindowsSwapChain : ISwapChain
{
    private readonly nint _device;
    private readonly ResourceManager? _resourceManager;
    private readonly nint _deviceContext;
    private nint _swapChain;
    private readonly void** _swapChainVTable;
    private int _width;
    private int _height;
    private WindowsGpuTexture _backBuffer;
    private int _disposed;

    public int Width => _width;
    public int Height => _height;
    public IGpuTexture CurrentBackBuffer => _backBuffer;

    public WindowsSwapChain(
        nint device,
        nint swapChain,
        int width,
        int height,
        ResourceManager? resourceManager,
        nint deviceContext)
    {
        _device = device;
        _swapChain = swapChain;
        _swapChainVTable = *(void***)_swapChain;
        _width = width;
        _height = height;
        _resourceManager = resourceManager;
        _deviceContext = deviceContext;

        _backBuffer = CreateBackBufferTexture(_resourceManager);
    }

    public void Present(bool vsync = true)
    {
        ThrowIfDisposed();

        uint syncInterval = vsync ? 1u : 0u;
        uint flags = 0;

        // slot 8 = Present
        var presentFn = (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)_swapChainVTable[8];
        int hr = presentFn(_swapChain, syncInterval, flags);

        if (hr < 0)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsDeviceLost,
                $"DXGI SwapChain Present failed (0x{hr:X8}). Device might have been lost.");
        }
    }

    public void Resize(int width, int height)
    {
        ThrowIfDisposed();
        if (width <= 0 || height <= 0) return;
        if (width == _width && height == _height) return;

        // Dispose existing back buffer RTV before resizing
        _backBuffer.Dispose();

        // slot 13 = ResizeBuffers(BufferCount, Width, Height, NewFormat, SwapChainFlags)
        var resizeFn = (delegate* unmanaged[Stdcall]<nint, uint, uint, uint, int, uint, int>)_swapChainVTable[13];
        int hr = resizeFn(_swapChain, 2, (uint)width, (uint)height, (int)GpuFormat.R8G8B8A8_UNorm, 0);

        if (hr < 0)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsSwapchainCreationFailed,
                $"DXGI SwapChain ResizeBuffers failed (0x{hr:X8}).");
        }

        _width = width;
        _height = height;
        _backBuffer = CreateBackBufferTexture(_resourceManager);
    }

    private WindowsGpuTexture CreateBackBufferTexture(ResourceManager? manager)
    {
        // slot 9 = GetBuffer(0, IID_ID3D11Texture2D, &pBackBuffer)
        var getBuffer = (delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int>)_swapChainVTable[9];
        var texGuid = D3D11Native.IID_ID3D11Texture2D;
        nint pBackBuffer = nint.Zero;
        int hr = getBuffer(_swapChain, 0, in texGuid, &pBackBuffer);

        if (hr < 0 || pBackBuffer == nint.Zero)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsSwapchainCreationFailed,
                $"Failed to retrieve backbuffer from swapchain (0x{hr:X8}).");
        }

        // Create RTV: slot 9 on ID3D11Device
        var deviceVTable = *(void***)_device;
        var createRtv = (delegate* unmanaged[Stdcall]<nint, nint, void*, nint*, int>)deviceVTable[9];
        nint pRtv = nint.Zero;
        hr = createRtv(_device, pBackBuffer, null, &pRtv);
        if (hr < 0 || pRtv == nint.Zero)
        {
            D3D11Native.SafeRelease(ref pBackBuffer);
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsSwapchainCreationFailed,
                $"Failed to create backbuffer RTV (0x{hr:X8}).");
        }

        return new WindowsGpuTexture(
            "SwapChainBackBuffer",
            pBackBuffer,
            _width,
            _height,
            GpuFormat.R8G8B8A8_UNorm,
            TextureUsage.RenderTarget,
            pRtv,
            nint.Zero,
            manager,
            default,
            _deviceContext);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _backBuffer.Dispose();
            D3D11Native.SafeRelease(ref _swapChain);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.InvalidOperation,
                "SwapChain has been disposed.");
        }
    }
}
