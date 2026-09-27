using Khefest.Audio;
using Khefest.Core.Assets;

namespace Khefest.Audio.Assets;

/// <summary>
/// Asset loader for RIFF/WAVE (.wav) audio files.
/// </summary>
public sealed class WavAssetLoader : IAssetLoader<AudioClip>
{
    private static readonly string[] Extensions = [".wav"];
    private readonly IAudioDevice _audioDevice;

    public IReadOnlyList<string> SupportedExtensions => Extensions;
    public Type AssetType => typeof(AudioClip);

    public WavAssetLoader(IAudioDevice audioDevice)
    {
        _audioDevice = audioDevice ?? throw new ArgumentNullException(nameof(audioDevice));
    }

    public AudioClip Load(Stream stream, string assetPath, IAssetContext context)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string clipName = string.IsNullOrEmpty(assetPath) ? "AudioClip" : Path.GetFileNameWithoutExtension(assetPath);
        return _audioDevice.CreateClipFromWave(clipName, stream);
    }

    public Task<AudioClip> LoadAsync(Stream stream, string assetPath, IAssetContext context)
    {
        return Task.FromResult(Load(stream, assetPath, context));
    }
}
