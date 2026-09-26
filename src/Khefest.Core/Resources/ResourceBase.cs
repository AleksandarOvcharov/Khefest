using System.Diagnostics;
using Khefest.Core.Errors;

namespace Khefest.Core.Resources;

/// <summary>
/// Foundation interface for all Khefest resources (GPU buffers, textures, shaders, audio, fonts).
/// </summary>
public interface IResource : IDisposable
{
    ResourceId Id { get; }
    string Name { get; }
    ResourceType Type { get; }
    ResourceState State { get; }
    long SizeInBytes { get; }
    bool IsDisposed { get; }
    string? AllocationStackTrace { get; }
}

/// <summary>
/// Abstract base class for all Khefest resources providing thread-safe disposal,
/// state transitions, size accounting, and optional allocation callstack capture.
/// </summary>
public abstract class ResourceBase : IResource
{
    private int _disposed;

    public ResourceId Id { get; }
    public string Name { get; }
    public ResourceType Type { get; }
    public ResourceState State { get; private set; }
    public long SizeInBytes { get; protected set; }
    public string? AllocationStackTrace { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    protected ResourceBase(
        string name,
        ResourceType type,
        long sizeInBytes = 0,
        bool captureStackTrace = false)
    {
        Id = ResourceId.Generate();
        Name = string.IsNullOrWhiteSpace(name) ? $"{type}_{Id.Value}" : name;
        Type = type;
        SizeInBytes = Math.Max(0, sizeInBytes);
        State = ResourceState.Allocated;

        if (captureStackTrace)
        {
            AllocationStackTrace = new StackTrace(1, true).ToString();
        }
    }

    public void MarkActive()
    {
        ThrowIfDisposed();
        State = ResourceState.Active;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            State = ResourceState.Disposed;
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    protected abstract void Dispose(bool disposing);

    protected void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw KhefestResourceException.AlreadyDisposed(Name, Id.Value);
        }
    }

    ~ResourceBase()
    {
        Dispose(false);
    }
}
