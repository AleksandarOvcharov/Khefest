namespace Khefest.Core.Plugins;

/// <summary>
/// Represents the lifecycle state of a plugin within the engine.
/// </summary>
public enum PluginState
{
    /// <summary>Plugin metadata has been discovered but assembly is not loaded.</summary>
    Discovered,

    /// <summary>Plugin assembly has been loaded into memory.</summary>
    Loaded,

    /// <summary>Plugin has completed Initialize() and registered services and extension points.</summary>
    Initialized,

    /// <summary>Plugin is active and running.</summary>
    Active,

    /// <summary>Plugin has been stopped and paused.</summary>
    Stopped,

    /// <summary>Plugin has been shut down and unloaded from memory.</summary>
    Unloaded,

    /// <summary>Plugin encountered an unhandled error during its lifecycle.</summary>
    Faulted
}
