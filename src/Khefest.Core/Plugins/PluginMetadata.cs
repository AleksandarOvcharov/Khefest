namespace Khefest.Core.Plugins;

/// <summary>
/// Specifies a required dependency on another plugin.
/// </summary>
public sealed record PluginDependency(string PluginId, Version MinimumVersion);

/// <summary>
/// Immutable descriptor containing identification, versioning, author, and dependency details for a plugin.
/// </summary>
public sealed record PluginMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Version Version { get; init; } = new(1, 0, 0);
    public string Author { get; init; } = "Unknown";
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<PluginDependency> Dependencies { get; init; } = [];

    public override string ToString() => $"{Name} ({Id}) v{Version} by {Author}";
}
