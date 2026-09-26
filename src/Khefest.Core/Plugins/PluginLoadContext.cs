using System.Reflection;
using System.Runtime.Loader;

namespace Khefest.Core.Plugins;

/// <summary>
/// Collectible isolated assembly load context for dynamically loading and unloading Khefest plugins.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public string PluginPath { get; }

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        PluginPath = pluginPath ?? throw new ArgumentNullException(nameof(pluginPath));
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 1. Resolve host assemblies via default load context to ensure shared type identity
        if (assemblyName.Name != null &&
            (assemblyName.Name.StartsWith("Khefest", StringComparison.OrdinalIgnoreCase) ||
             assemblyName.Name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
             assemblyName.Name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var defaultAsm = Default.LoadFromAssemblyName(assemblyName);
                if (defaultAsm != null) return defaultAsm;
            }
            catch
            {
                // Fall back to resolving locally
            }
        }

        // 2. Resolve assembly path from plugin directory
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
