using Khefest.Core.Errors;
using System.Diagnostics.CodeAnalysis;
using Khefest.Core.Logging;

namespace Khefest.Core.Resources;

/// <summary>
/// Thread-safe generic LRU resource cache with memory budget limits and automatic eviction.
/// </summary>
/// <typeparam name="TKey">Cache lookup key type.</typeparam>
/// <typeparam name="TResource">Cached resource type implementing <see cref="IResource"/>.</typeparam>
public sealed class ResourceCache<TKey, TResource> : IDisposable
    where TKey : notnull
    where TResource : class, IResource
{
    private sealed class CacheEntry
    {
        public required TKey Key { get; init; }
        public required TResource Resource { get; init; }
        public long LastAccessTick { get; set; }
    }

    private readonly Dictionary<TKey, CacheEntry> _entries = new();
    private readonly Lock _syncLock = new();
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Resource, $"Cache<{typeof(TResource).Name}>");
    private long _totalAllocatedBytes;
    private long _maxBudgetBytes;
    private long _tickCounter;
    private bool _disposed;

    public long TotalAllocatedBytes
    {
        get
        {
            lock (_syncLock) return _totalAllocatedBytes;
        }
    }

    public long MaxBudgetBytes
    {
        get
        {
            lock (_syncLock) return _maxBudgetBytes;
        }
        set
        {
            lock (_syncLock)
            {
                _maxBudgetBytes = value;
                if (_maxBudgetBytes > 0 && _totalAllocatedBytes > _maxBudgetBytes)
                {
                    TrimToBudgetInternal(_maxBudgetBytes);
                }
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_syncLock) return _entries.Count;
        }
    }

    public ResourceCache(long maxBudgetBytes = 0)
    {
        _maxBudgetBytes = maxBudgetBytes;
    }

    /// <summary>
    /// Attempts to retrieve a resource by key, updating its LRU timestamp.
    /// </summary>
    public bool TryGet(TKey key, [NotNullWhen(true)] out TResource? resource)
    {
        ThrowIfDisposed();
        lock (_syncLock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.Resource.IsDisposed)
                {
                    _entries.Remove(key);
                    _totalAllocatedBytes -= entry.Resource.SizeInBytes;
                    resource = null;
                    return false;
                }

                entry.LastAccessTick = ++_tickCounter;
                resource = entry.Resource;
                return true;
            }

            resource = null;
            return false;
        }
    }

    /// <summary>
    /// Gets an existing cached resource or creates a new one using the provided factory.
    /// </summary>
    public TResource GetOrCreate(TKey key, Func<TKey, TResource> factory)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(factory);

        lock (_syncLock)
        {
            if (TryGet(key, out var existing))
            {
                return existing;
            }

            var created = factory(key);
            AddInternal(key, created);
            return created;
        }
    }

    /// <summary>
    /// Asynchronously gets an existing cached resource or creates a new one using the provided asynchronous factory.
    /// </summary>
    public async Task<TResource> GetOrCreateAsync(TKey key, Func<TKey, Task<TResource>> factory)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(factory);

        lock (_syncLock)
        {
            if (TryGet(key, out var existing))
            {
                return existing;
            }
        }

        var created = await factory(key).ConfigureAwait(false);

        lock (_syncLock)
        {
            if (TryGet(key, out var existing))
            {
                // Already added concurrently; dispose newly created resource
                created.Dispose();
                return existing;
            }

            AddInternal(key, created);
            return created;
        }
    }

    /// <summary>
    /// Adds or replaces a resource in the cache.
    /// </summary>
    public void Add(TKey key, TResource resource)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(resource);

        lock (_syncLock)
        {
            AddInternal(key, resource);
        }
    }

    private void AddInternal(TKey key, TResource resource)
    {
        if (_entries.TryGetValue(key, out var oldEntry))
        {
            _totalAllocatedBytes -= oldEntry.Resource.SizeInBytes;
            if (!ReferenceEquals(oldEntry.Resource, resource))
            {
                oldEntry.Resource.Dispose();
            }
        }

        _entries[key] = new CacheEntry
        {
            Key = key,
            Resource = resource,
            LastAccessTick = ++_tickCounter
        };
        _totalAllocatedBytes += resource.SizeInBytes;

        if (_maxBudgetBytes > 0 && _totalAllocatedBytes > _maxBudgetBytes)
        {
            TrimToBudgetInternal(_maxBudgetBytes);
        }
    }

    /// <summary>
    /// Removes and disposes a cached resource by key.
    /// </summary>
    public bool Remove(TKey key)
    {
        ThrowIfDisposed();
        lock (_syncLock)
        {
            if (_entries.Remove(key, out var entry))
            {
                _totalAllocatedBytes -= entry.Resource.SizeInBytes;
                entry.Resource.Dispose();
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Evicts least recently used resources until total allocated bytes is within the specified budget.
    /// </summary>
    public int TrimToBudget(long maxBytes)
    {
        ThrowIfDisposed();
        lock (_syncLock)
        {
            return TrimToBudgetInternal(maxBytes);
        }
    }

    private int TrimToBudgetInternal(long maxBytes)
    {
        if (_totalAllocatedBytes <= maxBytes) return 0;

        // Sort entries by LastAccessTick ascending (LRU first)
        var lruCandidates = _entries.Values
            .OrderBy(e => e.LastAccessTick)
            .ToList();

        int evictedCount = 0;
        foreach (var entry in lruCandidates)
        {
            if (_totalAllocatedBytes <= maxBytes) break;

            _entries.Remove(entry.Key);
            _totalAllocatedBytes -= entry.Resource.SizeInBytes;
            entry.Resource.Dispose();
            evictedCount++;
        }

        _logger.Info($"Trimmed cache to budget ({maxBytes} bytes): evicted {evictedCount} items, current size: {_totalAllocatedBytes} bytes.");
        return evictedCount;
    }

    /// <summary>
    /// Disposes and clears all cached entries.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        lock (_syncLock)
        {
            foreach (var entry in _entries.Values)
            {
                entry.Resource.Dispose();
            }
            _entries.Clear();
            _totalAllocatedBytes = 0;
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var entry in _entries.Values)
            {
                entry.Resource.Dispose();
            }
            _entries.Clear();
            _totalAllocatedBytes = 0;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ResourceCache<TKey, TResource>));
        }
    }
}
