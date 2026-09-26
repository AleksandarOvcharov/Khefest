namespace Khefest.Core.Resources;

/// <summary>
/// Scoped lifetime boundary for transient resources.
/// All resources registered with this scope are disposed upon scope disposal in reverse order of registration.
/// </summary>
public sealed class ResourceScope : IDisposable
{
    private readonly Stack<IDisposable> _resources = new();
    private readonly ResourceManager? _manager;
    private readonly Lock _lock = new();
    private bool _disposed;

    public ResourceScope(ResourceManager? manager = null)
    {
        _manager = manager;
    }

    /// <summary>
    /// Enrolls a disposable resource into this scope.
    /// </summary>
    public T Track<T>(T resource) where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_lock)
        {
            if (_disposed)
            {
                resource.Dispose();
                throw new ObjectDisposedException(nameof(ResourceScope));
            }

            _resources.Push(resource);
            return resource;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            while (_resources.TryPop(out var resource))
            {
                try
                {
                    resource.Dispose();
                }
                catch
                {
                    // Continue disposing subsequent resources even if one throws
                }
            }
        }
    }
}
