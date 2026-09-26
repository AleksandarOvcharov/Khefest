using Khefest.Core.Assets;
using Khefest.Core.Errors;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Texturing;

namespace Khefest.Graphics.Assets;

/// <summary>
/// Asset loader for 2D textures supporting PNG, JPEG, BMP, and TIFF image formats.
/// </summary>
public sealed class Texture2DAssetLoader : IAssetLoader<Texture2D>
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".bmp", ".tiff"];

    private readonly IGpuDevice _device;

    public IReadOnlyList<string> SupportedExtensions => Extensions;
    public Type AssetType => typeof(Texture2D);

    public Texture2DAssetLoader(IGpuDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public Texture2D Load(Stream stream, string assetPath, IAssetContext context)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // If the asset path exists directly on disk, use high-speed direct file decoding
        if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
        {
            var decoded = WicImageDecoder.DecodeFromFile(assetPath);
            string textureName = Path.GetFileNameWithoutExtension(assetPath);
            return Texture2D.FromRgba(_device, textureName, decoded.Width, decoded.Height, decoded.Pixels, context.Resources);
        }

        // Otherwise write stream to memory temp file for WIC decoding
        string tempFile = Path.Combine(Path.GetTempPath(), $"khefest_asset_{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fileStream = File.Create(tempFile))
            {
                stream.CopyTo(fileStream);
            }

            var decoded = WicImageDecoder.DecodeFromFile(tempFile);
            string textureName = string.IsNullOrEmpty(assetPath) ? "Texture" : Path.GetFileNameWithoutExtension(assetPath);
            return Texture2D.FromRgba(_device, textureName, decoded.Width, decoded.Height, decoded.Pixels, context.Resources);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
            }
        }
    }

    public async Task<Texture2D> LoadAsync(Stream stream, string assetPath, IAssetContext context)
    {
        // Offload decode and upload to task
        return await Task.Run(() => Load(stream, assetPath, context)).ConfigureAwait(false);
    }
}
