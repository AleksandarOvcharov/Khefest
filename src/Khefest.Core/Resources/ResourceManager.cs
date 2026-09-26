using System.Collections.Concurrent;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;

namespace Khefest.Core.Resources;

/// <summary>
/// Central registry and coordinator for Khefest resources.
/// Supports both Automatic (<see cref="AutoMemManagement"/> = true) and
/// Manual (<see cref="AutoMemManagement"/> = false) resource management modes.
/// </summary>
public sealed class ResourceManager : IDisposable
{
    private readonly ConcurrentDictionary<ResourceId, IResource> _resources = new();
    private readonly ResourceLifetimeTracker _tracker;
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Resource, "ResourceManager");
    private int _disposed;

    /// <summary>
    /// When true, Khefest automatically releases registered resources upon manager shutdown.
    /// When false, unmanaged leaks are flagged and throw exceptions during strict verification.
    /// </summary>
    public bool AutoMemManagement { get; set; }

    public ResourceLifetimeTracker Tracker => _tracker;

    public ResourceManager(MemoryConfig? config = null)
    {
        config ??= new MemoryConfig();
        AutoMemManagement = config.AutoMemManagement;
        _tracker = new ResourceLifetimeTracker(config.EnableLeakTracking);

        _logger.Info($"ResourceManager initialized (AutoMemManagement: {AutoMemManagement}, LeakTracking: {config.EnableLeakTracking})");
    }

    /// <summary>
    /// Registers an allocated resource into the system and returns its typed handle.
    /// </summary>
    public ResourceHandle<T> Register<T>(T resource) where T : class, IResource
    {
        ArgumentNullException.ThrowIfNull(resource);
        ThrowIfDisposed();

        if (!_resources.TryAdd(resource.Id, resource))
        {
            throw new KhefestResourceException(
                KhefestErrorCode.ResourceAllocationFailed,
                $"Failed to register resource with duplicate ID: {resource.Id}");
        }

        _tracker.Track(resource);
        return new ResourceHandle<T>(resource.Id);
    }

    /// <summary>
    /// Retrieves a resource by handle. Throws if the resource does not exist or has been disposed.
    /// </summary>
    public T Get<T>(ResourceHandle<T> handle) where T : class, IResource
    {
        ThrowIfDisposed();

        if (!handle.IsValid)
        {
            throw new KhefestResourceException(
                KhefestErrorCode.ResourceInvalidHandle,
                $"Cannot resolve invalid handle: {handle}");
        }

        if (!_resources.TryGetValue(handle.Id, out var resource))
        {
            throw new KhefestResourceException(
                KhefestErrorCode.ResourceNotFound,
                $"Resource with ID {handle.Id} was not found in registry.");
        }

        if (resource.IsDisposed)
        {
            throw KhefestResourceException.AlreadyDisposed(resource.Name, resource.Id.Value);
        }

        if (resource is not T typedResource)
        {
            throw new KhefestResourceException(
                KhefestErrorCode.InvalidOperation,
                $"Resource {handle.Id} is of type '{resource.GetType().Name}', expected '{typeof(T).Name}'.");
        }

        return typedResource;
    }

    /// <summary>
    /// Releases and disposes a resource explicitly.
    /// </summary>
    public bool Release<T>(ResourceHandle<T> handle) where T : class, IResource
    {
        ThrowIfDisposed();

        if (!handle.IsValid)
        {
            return false;
        }

        if (_resources.TryRemove(handle.Id, out var resource))
        {
            _tracker.Untrack(resource.Id);
            resource.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Queries the current resource statistics across all registered types.
    /// </summary>
    public ResourceStatistics GetStatistics()
    {
        var counts = new Dictionary<ResourceType, int>();
        var bytes = new Dictionary<ResourceType, long>();
        long totalBytes = 0;
        int totalCount = 0;

        foreach (var r in _resources.Values)
        {
            if (r.IsDisposed)
            {
                continue;
            }

            totalCount++;
            totalBytes += r.SizeInBytes;

            counts[r.Type] = counts.GetValueOrDefault(r.Type) + 1;
            bytes[r.Type] = bytes.GetValueOrDefault(r.Type) + r.SizeInBytes;
        }

        return new ResourceStatistics
        {
            TotalResourceCount = totalCount,
            TotalAllocatedBytes = totalBytes,
            CountsByType = counts,
            BytesByType = bytes
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (AutoMemManagement)
        {
            _logger.Info("AutoMemManagement enabled: releasing all remaining resources...");
            foreach (var kv in _resources)
            {
                _tracker.Untrack(kv.Key);
                kv.Value.Dispose();
            }
            _resources.Clear();
        }
        else
        {
            _logger.Info("Manual resource management mode: validating that all resources were explicitly disposed...");
            _tracker.EnforceNoLeaks();
            _resources.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new KhefestResourceException(
                KhefestErrorCode.InvalidOperation,
                "ResourceManager has already been disposed.");
        }
    }
}
