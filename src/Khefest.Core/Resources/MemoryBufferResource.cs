using System.Runtime.InteropServices;

namespace Khefest.Core.Resources;

/// <summary>
/// A managed memory buffer resource representing raw host memory or staging allocations.
/// </summary>
public sealed class MemoryBufferResource : ResourceBase
{
    private IntPtr _nativeMemory;

    public IntPtr NativePointer
    {
        get
        {
            ThrowIfDisposed();
            return _nativeMemory;
        }
    }

    public MemoryBufferResource(string name, long sizeInBytes, bool captureStackTrace = true)
        : base(name, ResourceType.Buffer, sizeInBytes, captureStackTrace)
    {
        if (sizeInBytes > 0)
        {
            _nativeMemory = Marshal.AllocHGlobal((IntPtr)sizeInBytes);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_nativeMemory != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_nativeMemory);
            _nativeMemory = IntPtr.Zero;
        }
    }
}
