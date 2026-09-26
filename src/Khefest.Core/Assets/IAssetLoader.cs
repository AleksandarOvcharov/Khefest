namespace Khefest.Core.Assets;

/// <summary>
/// Non-generic base interface for asset loaders.
/// </summary>
public interface IAssetLoader
{
    /// <summary>
    /// File extensions supported by this loader (e.g. ".png", ".jpg"). Case-insensitive, including leading dot.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Type of the asset produced by this loader.
    /// </summary>
    Type AssetType { get; }
}

/// <summary>
/// Strongly-typed loader capable of parsing and instantiating an asset of type <typeparamref name="T"/>.
/// </summary>
public interface IAssetLoader<T> : IAssetLoader where T : class
{
    /// <summary>
    /// Loads the asset synchronously from the given data stream.
    /// </summary>
    T Load(Stream stream, string assetPath, IAssetContext context);

    /// <summary>
    /// Loads the asset asynchronously from the given data stream.
    /// </summary>
    Task<T> LoadAsync(Stream stream, string assetPath, IAssetContext context);
}
