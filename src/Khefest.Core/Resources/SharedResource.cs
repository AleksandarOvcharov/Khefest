using Khefest.Core.Errors;

namespace Khefest.Core.Resources;

/// <summary>
/// Reference-counted wrapper for automatic lifetime management of unmanaged or GPU resources.
/// When the last reference is disposed, the underlying resource is automatically disposed.
/// </summary>
/// <typeparam name="T">Type of the wrapped resource.</typeparam>
public sealed class SharedResource<T> : IDisposable where T : class, IDisposable
{
    private sealed class ControlBlock
    {
        public readonly T Instance;
        private int _refCount;

        public int RefCount => Volatile.Read(ref _refCount);

        public ControlBlock(T instance)
        {
            Instance = instance;
            _refCount = 1;
        }

        public void AddRef()
        {
            Interlocked.Increment(ref _refCount);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                Instance.Dispose();
            }
        }
    }

    private readonly ControlBlock _control;
    private int _disposed;

    public T Resource
    {
        get
        {
            ThrowIfDisposed();
            return _control.Instance;
        }
    }

    public int ReferenceCount => _control.RefCount;

    public SharedResource(T resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _control = new ControlBlock(resource);
    }

    private SharedResource(ControlBlock control)
    {
        _control = control;
        _control.AddRef();
    }

    /// <summary>
    /// Creates an additional shared reference incrementing the reference counter.
    /// </summary>
    public SharedResource<T> Acquire()
    {
        ThrowIfDisposed();
        return new SharedResource<T>(_control);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _control.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SharedResource<T>));
        }
    }
}
