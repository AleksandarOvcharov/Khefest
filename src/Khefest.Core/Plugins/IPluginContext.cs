using Khefest.Core.Logging;

namespace Khefest.Core.Plugins;

/// <summary>
/// Execution environment and services provided to a plugin during its lifecycle.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Metadata descriptor of the active plugin.
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Extension point registry for exposing or consuming modular extensions.
    /// </summary>
    IExtensionRegistry Extensions { get; }

    /// <summary>
    /// Dedicated logger instance for this plugin.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Resolves an optional host service of type <typeparamref name="TService"/>.
    /// </summary>
    TService? GetService<TService>() where TService : class;

    /// <summary>
    /// Shared inter-plugin property bag.
    /// </summary>
    IDictionary<string, object?> Properties { get; }
}
