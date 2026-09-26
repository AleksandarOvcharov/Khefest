namespace Khefest.Core.Plugins;

/// <summary>
/// Registry for discovering, registering, and querying decoupled extension points and services.
/// </summary>
public interface IExtensionRegistry
{
    /// <summary>
    /// Registers an extension instance of type <typeparamref name="TExtension"/>.
    /// </summary>
    void Register<TExtension>(TExtension extension) where TExtension : class;

    /// <summary>
    /// Unregisters an extension instance of type <typeparamref name="TExtension"/>.
    /// </summary>
    bool Unregister<TExtension>(TExtension extension) where TExtension : class;

    /// <summary>
    /// Returns all registered extensions of type <typeparamref name="TExtension"/>.
    /// </summary>
    IReadOnlyList<TExtension> GetAll<TExtension>() where TExtension : class;

    /// <summary>
    /// Returns the first registered extension of type <typeparamref name="TExtension"/>, or null if none registered.
    /// </summary>
    TExtension? Get<TExtension>() where TExtension : class;
}

/// <summary>
/// Thread-safe implementation of <see cref="IExtensionRegistry"/>.
/// </summary>
public sealed class ExtensionRegistry : IExtensionRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Type, List<object>> _extensions = new();

    public void Register<TExtension>(TExtension extension) where TExtension : class
    {
        ArgumentNullException.ThrowIfNull(extension);

        lock (_lock)
        {
            var type = typeof(TExtension);
            if (!_extensions.TryGetValue(type, out var list))
            {
                list = [];
                _extensions[type] = list;
            }

            if (!list.Contains(extension))
            {
                list.Add(extension);
            }
        }
    }

    public bool Unregister<TExtension>(TExtension extension) where TExtension : class
    {
        ArgumentNullException.ThrowIfNull(extension);

        lock (_lock)
        {
            var type = typeof(TExtension);
            if (_extensions.TryGetValue(type, out var list))
            {
                bool removed = list.Remove(extension);
                if (list.Count == 0)
                {
                    _extensions.Remove(type);
                }
                return removed;
            }
            return false;
        }
    }

    public IReadOnlyList<TExtension> GetAll<TExtension>() where TExtension : class
    {
        lock (_lock)
        {
            if (_extensions.TryGetValue(typeof(TExtension), out var list))
            {
                return list.Cast<TExtension>().ToList();
            }
            return [];
        }
    }

    public TExtension? Get<TExtension>() where TExtension : class
    {
        lock (_lock)
        {
            if (_extensions.TryGetValue(typeof(TExtension), out var list) && list.Count > 0)
            {
                return (TExtension)list[0];
            }
            return null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _extensions.Clear();
        }
    }
}
