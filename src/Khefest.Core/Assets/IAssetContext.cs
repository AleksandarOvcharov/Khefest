using Khefest.Core.Resources;

namespace Khefest.Core.Assets;

/// <summary>
/// Execution context provided to asset loaders during asset deserialization.
/// </summary>
public interface IAssetContext
{
    ResourceManager? Resources { get; }
    IServiceProvider? Services { get; }
    T? GetService<T>() where T : class;
}
