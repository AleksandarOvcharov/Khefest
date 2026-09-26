using System.Collections.Concurrent;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;

namespace Khefest.Core.Assets;

/// <summary>
/// Central manager for loading, caching, and unloading game assets with support for synchronous and asynchronous loading.
/// </summary>
public sealed class AssetManager : IAssetContext, IDisposable
{
    private sealed class CachedAssetEntry
    {
        public required object Asset { get; init; }
        public required Type AssetType { get; init; }
    }

    private readonly ConcurrentDictionary<string, IAssetLoader> _loadersByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedAssetEntry> _loadedAssets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ResourceManager? _resources;
    private readonly IServiceProvider? _services;
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Resource, "AssetManager");
    private string _rootDirectory;
    private bool _disposed;

    public string RootDirectory
    {
        get => _rootDirectory;
        set => _rootDirectory = Path.GetFullPath(value);
    }

    public ResourceManager? Resources => _resources;
    public IServiceProvider? Services => _services;

    public AssetManager(string? rootDirectory = null, ResourceManager? resources = null, IServiceProvider? services = null)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory ?? AppContext.BaseDirectory);
        _resources = resources;
        _services = services;
        _logger.Info($"AssetManager initialized at '{_rootDirectory}'.");
    }

    public T? GetService<T>() where T : class
    {
        return _services?.GetService(typeof(T)) as T;
    }

    /// <summary>
    /// Registers a custom asset loader for its supported file extensions.
    /// </summary>
    public void RegisterLoader<T>(IAssetLoader<T> loader) where T : class
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(loader);

        foreach (var ext in loader.SupportedExtensions)
        {
            var normalizedExt = ext.StartsWith('.') ? ext : "." + ext;
            _loadersByExtension[normalizedExt] = loader;
            _logger.Debug($"Registered asset loader '{loader.GetType().Name}' for extension '{normalizedExt}'.");
        }
    }

    /// <summary>
    /// Loads an asset synchronously from the filesystem, returning a cached instance if already loaded.
    /// </summary>
    public T Load<T>(string assetPath) where T : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        string normalizedKey = NormalizePath(assetPath);

        if (_loadedAssets.TryGetValue(normalizedKey, out var existing))
        {
            if (existing.Asset is T typedAsset)
            {
                return typedAsset;
            }

            throw new KhefestResourceException(
                KhefestErrorCode.InvalidOperation,
                $"Asset at '{assetPath}' is already loaded as '{existing.AssetType.Name}', not '{typeof(T).Name}'.");
        }

        string fullPath = ResolveFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Asset file not found at '{fullPath}'.", fullPath);
        }

        var ext = Path.GetExtension(fullPath);
        if (!_loadersByExtension.TryGetValue(ext, out var loader) || loader is not IAssetLoader<T> typedLoader)
        {
            throw new KhefestResourceException(
                KhefestErrorCode.ResourceNotFound,
                $"No asset loader registered for extension '{ext}' producing type '{typeof(T).Name}'.");
        }

        using var stream = File.OpenRead(fullPath);
        var loaded = typedLoader.Load(stream, fullPath, this);

        _loadedAssets[normalizedKey] = new CachedAssetEntry
        {
            Asset = loaded,
            AssetType = typeof(T)
        };

        _logger.Info($"Loaded asset '{assetPath}' [{typeof(T).Name}].");
        return loaded;
    }

    /// <summary>
    /// Loads an asset asynchronously from the filesystem, returning a cached instance if already loaded.
    /// </summary>
    public async Task<T> LoadAsync<T>(string assetPath) where T : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        string normalizedKey = NormalizePath(assetPath);

        if (_loadedAssets.TryGetValue(normalizedKey, out var existing))
        {
            if (existing.Asset is T typedAsset)
            {
                return typedAsset;
            }

            throw new KhefestResourceException(
                KhefestErrorCode.InvalidOperation,
                $"Asset at '{assetPath}' is already loaded as '{existing.AssetType.Name}', not '{typeof(T).Name}'.");
        }

        string fullPath = ResolveFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Asset file not found at '{fullPath}'.", fullPath);
        }

        var ext = Path.GetExtension(fullPath);
        if (!_loadersByExtension.TryGetValue(ext, out var loader) || loader is not IAssetLoader<T> typedLoader)
        {
            throw new KhefestResourceException(
                KhefestErrorCode.ResourceNotFound,
                $"No asset loader registered for extension '{ext}' producing type '{typeof(T).Name}'.");
        }

        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var loaded = await typedLoader.LoadAsync(stream, fullPath, this).ConfigureAwait(false);

        _loadedAssets[normalizedKey] = new CachedAssetEntry
        {
            Asset = loaded,
            AssetType = typeof(T)
        };

        _logger.Info($"Loaded asset async '{assetPath}' [{typeof(T).Name}].");
        return loaded;
    }

    /// <summary>
    /// Unloads and disposes a cached asset by path if it is an <see cref="IDisposable"/>.
    /// </summary>
    public bool Unload(string assetPath)
    {
        ThrowIfDisposed();
        string normalizedKey = NormalizePath(assetPath);

        if (_loadedAssets.TryRemove(normalizedKey, out var entry))
        {
            if (entry.Asset is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _logger.Info($"Unloaded asset '{assetPath}'.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unloads and disposes all currently cached assets.
    /// </summary>
    public void UnloadAll()
    {
        ThrowIfDisposed();
        foreach (var entry in _loadedAssets.Values)
        {
            if (entry.Asset is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _loadedAssets.Clear();
        _logger.Info("Unloaded all cached assets.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _loadedAssets.Values)
        {
            if (entry.Asset is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _loadedAssets.Clear();
        _loadersByExtension.Clear();
    }

    private string ResolveFullPath(string assetPath)
    {
        if (Path.IsPathRooted(assetPath))
        {
            return Path.GetFullPath(assetPath);
        }

        return Path.GetFullPath(Path.Combine(_rootDirectory, assetPath));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('/', '\\').Trim().ToLowerInvariant();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AssetManager));
        }
    }
}
