using Khefest.Core.Resources;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native implementation of <see cref="IGpuBuffer"/> backed by Direct3D 11.
/// </summary>
public sealed unsafe class WindowsGpuBuffer : ResourceBase, IGpuBuffer
{
    private readonly nint _deviceContext;
    private nint _buffer;
    private readonly BufferUsage _usage;

    public nint NativeBuffer => _buffer;
    public BufferUsage Usage => _usage;

    public WindowsGpuBuffer(
        string name,
        nint buffer,
        long sizeInBytes,
        BufferUsage usage,
        nint deviceContext,
        ResourceManager? manager = null)
        : base(name, ResourceType.Buffer, sizeInBytes, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _buffer = buffer;
        _usage = usage;
        _deviceContext = deviceContext;
        manager?.Register(this);
    }

    public void SetData<T>(ReadOnlySpan<T> data, int offsetInBytes = 0) where T : unmanaged
    {
        ThrowIfDisposed();
        if (data.IsEmpty) return;

        var byteLength = data.Length * sizeof(T);
        if (offsetInBytes + byteLength > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Data length ({byteLength} bytes) at offset {offsetInBytes} exceeds buffer size ({SizeInBytes} bytes).");
        }

        fixed (T* pSrc = data)
        {
            // slot 48 = UpdateSubresource
            var contextVTable = *(void***)_deviceContext;
            var updateSubresource = (delegate* unmanaged[Stdcall]<nint, nint, uint, void*, void*, uint, uint, void>)contextVTable[48];
            updateSubresource(_deviceContext, _buffer, 0, null, pSrc, 0, 0);
        }
    }

    public void SetData<T>(in T value, int offsetInBytes = 0) where T : unmanaged
    {
        ThrowIfDisposed();

        var byteLength = sizeof(T);
        if (offsetInBytes + byteLength > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Data length ({byteLength} bytes) at offset {offsetInBytes} exceeds buffer size ({SizeInBytes} bytes).");
        }

        fixed (T* pSrc = &value)
        {
            // slot 48 = UpdateSubresource
            var contextVTable = *(void***)_deviceContext;
            var updateSubresource = (delegate* unmanaged[Stdcall]<nint, nint, uint, void*, void*, uint, uint, void>)contextVTable[48];
            updateSubresource(_deviceContext, _buffer, 0, null, pSrc, (uint)byteLength, 0);
        }
    }

    protected override void Dispose(bool disposing)
    {
        D3D11Native.SafeRelease(ref _buffer);
    }
}
