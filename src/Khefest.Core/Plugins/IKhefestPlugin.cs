namespace Khefest.Core.Plugins;

/// <summary>
/// Root interface implemented by all Khefest plugins and extensions.
/// </summary>
public interface IKhefestPlugin
{
    /// <summary>
    /// Metadata identifying this plugin.
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Current lifecycle state of the plugin.
    /// </summary>
    PluginState State { get; }

    /// <summary>
    /// Invoked upon loading to configure extensions, register services, and store the execution context.
    /// </summary>
    void Initialize(IPluginContext context);

    /// <summary>
    /// Invoked when the host application or engine starts execution.
    /// </summary>
    void Start();

    /// <summary>
    /// Invoked when the plugin is paused or temporarily stopped.
    /// </summary>
    void Stop();

    /// <summary>
    /// Invoked when the plugin is being shut down and prepared for unloading.
    /// </summary>
    void Shutdown();
}

/// <summary>
/// Convenience base class for implementing <see cref="IKhefestPlugin"/> with automatic state tracking.
/// </summary>
public abstract class PluginBase : IKhefestPlugin
{
    public abstract PluginMetadata Metadata { get; }

    public PluginState State { get; internal set; } = PluginState.Loaded;

    protected IPluginContext Context { get; private set; } = null!;

    public virtual void Initialize(IPluginContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        State = PluginState.Initialized;
    }

    public virtual void Start()
    {
        State = PluginState.Active;
    }

    public virtual void Stop()
    {
        State = PluginState.Stopped;
    }

    public virtual void Shutdown()
    {
        State = PluginState.Unloaded;
    }
}
