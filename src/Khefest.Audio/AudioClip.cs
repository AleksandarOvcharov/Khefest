using Khefest.Audio.Wave;
using Khefest.Core.Resources;

namespace Khefest.Audio;

/// <summary>
/// Represents a decoded, in-memory audio clip containing uncompressed PCM waveform data.
/// </summary>
public class AudioClip : ResourceBase
{
    private readonly byte[] _rawAudioData;
    private readonly ResourceManager? _manager;

    public int SampleRate { get; }
    public int Channels { get; }
    public AudioSampleFormat SampleFormat { get; }
    public int BitsPerSample { get; }
    public int SampleCount { get; }
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets a read-only memory view over the underlying PCM byte buffer.
    /// </summary>
    public ReadOnlyMemory<byte> AudioData => _rawAudioData;

    public AudioClip(
        string name,
        int sampleRate,
        int channels,
        AudioSampleFormat sampleFormat,
        int bitsPerSample,
        byte[] rawAudioData,
        ResourceManager? manager = null)
        : base(name, ResourceType.Audio, rawAudioData?.Length ?? 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentNullException.ThrowIfNull(rawAudioData);

        SampleRate = sampleRate;
        Channels = channels;
        SampleFormat = sampleFormat;
        BitsPerSample = bitsPerSample;
        _rawAudioData = rawAudioData;
        _manager = manager;

        int bytesPerFrame = channels * (bitsPerSample / 8);
        SampleCount = bytesPerFrame > 0 ? rawAudioData.Length / bytesPerFrame : 0;
        Duration = TimeSpan.FromSeconds(SampleRate > 0 ? (double)SampleCount / SampleRate : 0.0);

        _manager?.Tracker.Track(this);
    }

    /// <summary>
    /// Creates an <see cref="AudioClip"/> directly from parsed <see cref="WaveData"/>.
    /// </summary>
    public static AudioClip FromWaveData(string name, WaveData waveData, ResourceManager? manager = null)
    {
        ArgumentNullException.ThrowIfNull(waveData);
        return new AudioClip(
            name,
            waveData.SampleRate,
            waveData.Channels,
            waveData.SampleFormat,
            waveData.BitsPerSample,
            waveData.RawBytes,
            manager);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager?.Tracker.Untrack(Id);
        }
    }
}
