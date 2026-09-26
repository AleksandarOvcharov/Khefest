using Khefest.Core.Errors;
using Khefest.Core.Plugins;
using Xunit;

namespace Khefest.Tests;

public sealed class Phase10PluginTests
{
    private interface ITestToolExtension
    {
        string GetName();
    }

    private sealed class SampleToolExtension : ITestToolExtension
    {
        public string GetName() => "SampleTool";
    }

    private sealed class AlternateToolExtension : ITestToolExtension
    {
        public string GetName() => "AlternateTool";
    }

    private sealed class TestPlugin : PluginBase
    {
        public override PluginMetadata Metadata { get; }
        public List<string> ExecutionLog { get; } = [];

        public TestPlugin(string id, string name, Version? version = null, IReadOnlyList<PluginDependency>? deps = null)
        {
            Metadata = new PluginMetadata
            {
                Id = id,
                Name = name,
                Version = version ?? new Version(1, 0, 0),
                Dependencies = deps ?? []
            };
        }

        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);
            ExecutionLog.Add("Initialize");
        }

        public override void Start()
        {
            base.Start();
            ExecutionLog.Add("Start");
        }

        public override void Stop()
        {
            base.Stop();
            ExecutionLog.Add("Stop");
        }

        public override void Shutdown()
        {
            base.Shutdown();
            ExecutionLog.Add("Shutdown");
        }
    }

    [Fact]
    public void ExtensionRegistry_RegistersAndQueriesExtensionsCorrectly()
    {
        var registry = new ExtensionRegistry();
        var tool1 = new SampleToolExtension();
        var tool2 = new AlternateToolExtension();

        registry.Register<ITestToolExtension>(tool1);
        registry.Register<ITestToolExtension>(tool2);

        var all = registry.GetAll<ITestToolExtension>();
        Assert.Equal(2, all.Count);
        Assert.Contains(tool1, all);
        Assert.Contains(tool2, all);

        var first = registry.Get<ITestToolExtension>();
        Assert.NotNull(first);
        Assert.Equal("SampleTool", first.GetName());

        // Unregister
        bool removed = registry.Unregister<ITestToolExtension>(tool1);
        Assert.True(removed);

        all = registry.GetAll<ITestToolExtension>();
        Assert.Single(all);
        Assert.Equal(tool2, all[0]);
    }

    [Fact]
    public void PluginManager_RegistersAndOrchestratesLifecycle()
    {
        using var manager = new PluginManager();
        var plugin = new TestPlugin("plugin.test", "Test Plugin");

        manager.LoadPlugin(plugin);
        Assert.Single(manager.Plugins);
        Assert.Equal(PluginState.Loaded, plugin.State);

        manager.InitializeAll();
        Assert.Equal(PluginState.Initialized, plugin.State);
        Assert.Contains("Initialize", plugin.ExecutionLog);

        manager.StartAll();
        Assert.Equal(PluginState.Active, plugin.State);
        Assert.Contains("Start", plugin.ExecutionLog);

        manager.StopAll();
        Assert.Equal(PluginState.Stopped, plugin.State);
        Assert.Contains("Stop", plugin.ExecutionLog);

        manager.ShutdownAll();
        Assert.Equal(PluginState.Unloaded, plugin.State);
        Assert.Contains("Shutdown", plugin.ExecutionLog);
        Assert.Empty(manager.Plugins);
    }

    [Fact]
    public void PluginManager_ResolvesDependenciesInTopologicalOrder()
    {
        using var manager = new PluginManager();
        var initOrder = new List<string>();

        var pluginA = new TestPlugin("plugin.a", "Plugin A");
        var pluginB = new TestPlugin(
            "plugin.b",
            "Plugin B",
            deps: [new PluginDependency("plugin.a", new Version(1, 0, 0))]);
        var pluginC = new TestPlugin(
            "plugin.c",
            "Plugin C",
            deps: [new PluginDependency("plugin.b", new Version(1, 0, 0))]);

        // Register in reverse dependency order: C, then B, then A
        manager.LoadPlugin(pluginC);
        manager.LoadPlugin(pluginB);
        manager.LoadPlugin(pluginA);

        manager.InitializeAll();

        // Topological order must be A -> B -> C
        int idxA = manager.Plugins.ToList().IndexOf(pluginA);
        int idxB = manager.Plugins.ToList().IndexOf(pluginB);
        int idxC = manager.Plugins.ToList().IndexOf(pluginC);

        Assert.Equal(PluginState.Initialized, pluginA.State);
        Assert.Equal(PluginState.Initialized, pluginB.State);
        Assert.Equal(PluginState.Initialized, pluginC.State);
    }

    [Fact]
    public void PluginManager_DetectsCircularDependencies()
    {
        using var manager = new PluginManager();

        var pluginX = new TestPlugin(
            "plugin.x",
            "Plugin X",
            deps: [new PluginDependency("plugin.y", new Version(1, 0, 0))]);
        var pluginY = new TestPlugin(
            "plugin.y",
            "Plugin Y",
            deps: [new PluginDependency("plugin.x", new Version(1, 0, 0))]);

        manager.LoadPlugin(pluginX);
        manager.LoadPlugin(pluginY);

        var ex = Assert.Throws<KhefestPluginException>(() => manager.InitializeAll());
        Assert.Equal(KhefestErrorCode.PluginIncompatible, ex.ErrorCode);
    }

    [Fact]
    public void PluginManager_FailsGracefullyOnMissingDependency()
    {
        using var manager = new PluginManager();

        var plugin = new TestPlugin(
            "plugin.orphan",
            "Orphan Plugin",
            deps: [new PluginDependency("non.existent.dep", new Version(1, 0, 0))]);

        manager.LoadPlugin(plugin);
        manager.InitializeAll();

        Assert.Equal(PluginState.Faulted, plugin.State);
    }

    [Fact]
    public void PluginManager_UnloadPlugin_CleansUpAndFiresShutdown()
    {
        using var manager = new PluginManager();
        var plugin = new TestPlugin("plugin.removable", "Removable Plugin");

        manager.LoadPlugin(plugin);
        manager.InitializeAll();
        manager.StartAll();
        Assert.Equal(PluginState.Active, plugin.State);

        bool unloaded = manager.UnloadPlugin("plugin.removable");
        Assert.True(unloaded);
        Assert.Equal(PluginState.Unloaded, plugin.State);
        Assert.Empty(manager.Plugins);
    }
}
