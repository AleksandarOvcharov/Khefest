using System.Reflection;
using System.Runtime.CompilerServices;
using Khefest.Core.Errors;
using Khefest.Core.Logging;

namespace Khefest.Core.Plugins;

/// <summary>
/// Central manager for discovering, loading, validating dependencies, initializing, and orchestrating plugin lifecycles.
/// </summary>
public sealed class PluginManager : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "PluginManager");

    private readonly List<PluginRecord> _plugins = [];
    private readonly ExtensionRegistry _extensions = new();
    private readonly IServiceProvider? _hostServices;
    private readonly Lock _lock = new();

    public IReadOnlyList<IKhefestPlugin> Plugins
    {
        get
        {
            lock (_lock)
            {
                return _plugins.Select(p => p.Plugin).ToList();
            }
        }
    }

    public IExtensionRegistry Extensions => _extensions;

    private sealed class PluginRecord
    {
        public required IKhefestPlugin Plugin { get; init; }
        public PluginLoadContext? LoadContext { get; init; }
        public required PluginContext Context { get; init; }
    }

    private sealed class PluginContext : IPluginContext
    {
        public PluginMetadata Metadata { get; }
        public IExtensionRegistry Extensions { get; }
        public ILogger Logger { get; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

        private readonly IServiceProvider? _services;

        public PluginContext(PluginMetadata metadata, IExtensionRegistry extensions, IServiceProvider? services)
        {
            Metadata = metadata;
            Extensions = extensions;
            Logger = LogManager.GetLogger(KhefestSubsystem.Core, $"Plugin[{metadata.Id}]");
            _services = services;
        }

        public TService? GetService<TService>() where TService : class
        {
            return _services?.GetService(typeof(TService)) as TService;
        }
    }

    public PluginManager(IServiceProvider? hostServices = null)
    {
        _hostServices = hostServices;
    }

    /// <summary>
    /// Registers an in-memory instantiated plugin.
    /// </summary>
    public void LoadPlugin(IKhefestPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        lock (_lock)
        {
            if (_plugins.Any(p => p.Plugin.Metadata.Id.Equals(plugin.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new KhefestPluginException(
                    KhefestErrorCode.InvalidOperation,
                    $"A plugin with ID '{plugin.Metadata.Id}' is already registered.");
            }

            var context = new PluginContext(plugin.Metadata, _extensions, _hostServices);
            _plugins.Add(new PluginRecord
            {
                Plugin = plugin,
                LoadContext = null,
                Context = context
            });

            Logger.Info($"Registered in-memory plugin '{plugin.Metadata.Name}' ({plugin.Metadata.Id}) v{plugin.Metadata.Version}.");
        }
    }

    /// <summary>
    /// Dynamically loads a plugin from an assembly file into an isolated collectible load context.
    /// </summary>
    public IKhefestPlugin LoadPluginFromAssembly(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly file not found at '{assemblyPath}'.");
        }

        var loadContext = new PluginLoadContext(assemblyPath);
        Assembly assembly;

        try
        {
            assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception ex)
        {
            loadContext.Unload();
            throw new KhefestPluginException(
                KhefestErrorCode.PluginLoadFailed,
                $"Failed to load assembly from '{assemblyPath}'.",
                ex);
        }

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IKhefestPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            loadContext.Unload();
            throw new KhefestPluginException(
                KhefestErrorCode.PluginLoadFailed,
                $"No types implementing {nameof(IKhefestPlugin)} were found in '{assemblyPath}'.");
        }

        var pluginInstance = (IKhefestPlugin)Activator.CreateInstance(pluginTypes[0])!;

        lock (_lock)
        {
            if (_plugins.Any(p => p.Plugin.Metadata.Id.Equals(pluginInstance.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
            {
                loadContext.Unload();
                throw new KhefestPluginException(
                    KhefestErrorCode.InvalidOperation,
                    $"A plugin with ID '{pluginInstance.Metadata.Id}' is already registered.");
            }

            var context = new PluginContext(pluginInstance.Metadata, _extensions, _hostServices);
            _plugins.Add(new PluginRecord
            {
                Plugin = pluginInstance,
                LoadContext = loadContext,
                Context = context
            });

            Logger.Info($"Loaded plugin '{pluginInstance.Metadata.Name}' ({pluginInstance.Metadata.Id}) from '{assemblyPath}'.");
            return pluginInstance;
        }
    }

    /// <summary>
    /// Scans a directory for all .dll files containing plugin implementations and loads them.
    /// </summary>
    public IReadOnlyList<IKhefestPlugin> DiscoverPlugins(string directoryPath, bool recursive = true)
    {
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        var discovered = new List<IKhefestPlugin>();
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var dllFiles = Directory.GetFiles(directoryPath, "*.dll", searchOption);

        foreach (var file in dllFiles)
        {
            try
            {
                var plugin = LoadPluginFromAssembly(file);
                discovered.Add(plugin);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Skipped file '{file}': {ex.Message}");
            }
        }

        return discovered;
    }

    /// <summary>
    /// Validates dependencies and initializes all loaded plugins in topological order.
    /// </summary>
    public void InitializeAll()
    {
        lock (_lock)
        {
            var sorted = TopologicalSortPlugins(_plugins);

            foreach (var record in sorted)
            {
                if (record.Plugin.State == PluginState.Faulted) continue;

                // Validate dependencies
                bool dependenciesSatisfied = true;
                foreach (var dep in record.Plugin.Metadata.Dependencies)
                {
                    var depRecord = _plugins.FirstOrDefault(p =>
                        p.Plugin.Metadata.Id.Equals(dep.PluginId, StringComparison.OrdinalIgnoreCase));

                    if (depRecord == null)
                    {
                        Logger.Error($"Plugin '{record.Plugin.Metadata.Id}' requires missing dependency '{dep.PluginId}'.");
                        dependenciesSatisfied = false;
                        break;
                    }

                    if (depRecord.Plugin.Metadata.Version < dep.MinimumVersion)
                    {
                        Logger.Error($"Plugin '{record.Plugin.Metadata.Id}' requires '{dep.PluginId}' >= {dep.MinimumVersion}, but found v{depRecord.Plugin.Metadata.Version}.");
                        dependenciesSatisfied = false;
                        break;
                    }
                }

                if (!dependenciesSatisfied)
                {
                    MarkFaulted(record.Plugin, "Dependencies not satisfied.");
                    continue;
                }

                try
                {
                    record.Plugin.Initialize(record.Context);
                    Logger.Info($"Initialized plugin '{record.Plugin.Metadata.Id}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Plugin '{record.Plugin.Metadata.Id}' failed during Initialize: {ex.Message}");
                    MarkFaulted(record.Plugin, ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Starts all initialized plugins.
    /// </summary>
    public void StartAll()
    {
        lock (_lock)
        {
            foreach (var record in _plugins)
            {
                if (record.Plugin.State == PluginState.Initialized || record.Plugin.State == PluginState.Stopped)
                {
                    try
                    {
                        record.Plugin.Start();
                        Logger.Info($"Started plugin '{record.Plugin.Metadata.Id}'.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Plugin '{record.Plugin.Metadata.Id}' failed during Start: {ex.Message}");
                        MarkFaulted(record.Plugin, ex.Message);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Stops all running plugins.
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var record in _plugins)
            {
                if (record.Plugin.State == PluginState.Active)
                {
                    try
                    {
                        record.Plugin.Stop();
                        Logger.Info($"Stopped plugin '{record.Plugin.Metadata.Id}'.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Plugin '{record.Plugin.Metadata.Id}' failed during Stop: {ex.Message}");
                        MarkFaulted(record.Plugin, ex.Message);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Shuts down all plugins and unloads assembly load contexts.
    /// </summary>
    public void ShutdownAll()
    {
        lock (_lock)
        {
            // Shutdown in reverse initialization order
            for (int i = _plugins.Count - 1; i >= 0; i--)
            {
                var record = _plugins[i];
                try
                {
                    record.Plugin.Shutdown();
                    Logger.Info($"Shut down plugin '{record.Plugin.Metadata.Id}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Plugin '{record.Plugin.Metadata.Id}' failed during Shutdown: {ex.Message}");
                }

                record.LoadContext?.Unload();
            }

            _plugins.Clear();
            _extensions.Clear();
        }
    }

    /// <summary>
    /// Unloads a specific plugin by its unique ID.
    /// </summary>
    public bool UnloadPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        lock (_lock)
        {
            var record = _plugins.FirstOrDefault(p =>
                p.Plugin.Metadata.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

            if (record == null) return false;

            try
            {
                if (record.Plugin.State == PluginState.Active)
                {
                    record.Plugin.Stop();
                }
                record.Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error($"Plugin '{pluginId}' failed during Unload: {ex.Message}");
            }

            record.LoadContext?.Unload();
            _plugins.Remove(record);

            Logger.Info($"Unloaded plugin '{pluginId}'.");
            return true;
        }
    }

    private static void MarkFaulted(IKhefestPlugin plugin, string reason)
    {
        if (plugin is PluginBase pb)
        {
            pb.State = PluginState.Faulted;
        }
    }

    private static List<PluginRecord> TopologicalSortPlugins(List<PluginRecord> plugins)
    {
        var sorted = new List<PluginRecord>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(PluginRecord record)
        {
            string id = record.Plugin.Metadata.Id;
            if (inStack.Contains(id))
            {
                throw new KhefestPluginException(
                    KhefestErrorCode.PluginIncompatible,
                    $"Circular dependency detected involving plugin '{id}'.");
            }

            if (!visited.Contains(id))
            {
                inStack.Add(id);

                foreach (var dep in record.Plugin.Metadata.Dependencies)
                {
                    var depRecord = plugins.FirstOrDefault(p =>
                        p.Plugin.Metadata.Id.Equals(dep.PluginId, StringComparison.OrdinalIgnoreCase));

                    if (depRecord != null)
                    {
                        Visit(depRecord);
                    }
                }

                inStack.Remove(id);
                visited.Add(id);
                sorted.Add(record);
            }
        }

        foreach (var record in plugins)
        {
            Visit(record);
        }

        return sorted;
    }

    public void Dispose()
    {
        ShutdownAll();
    }
}
