using Khefest.Audio.Wave;
using Khefest.Core.Resources;

namespace Khefest.Audio;

/// <summary>
/// Hardware or software audio device responsible for creating clips and managing playback sources.
/// </summary>
public interface IAudioDevice : IDisposable
{
    /// <summary>
    /// Master volume scale for all audio output across the device, clamped to [0.0, 1.0+].
    /// </summary>
    float MasterVolume { get; set; }

    /// <summary>
    /// Gets or sets whether global audio playback is muted.
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Creates an in-memory audio clip from raw PCM parameters.
    /// </summary>
    AudioClip CreateClip(
        string name,
        int sampleRate,
        int channels,
        AudioSampleFormat sampleFormat,
        int bitsPerSample,
        byte[] rawAudioData);

    /// <summary>
    /// Creates an in-memory audio clip by parsing a WAV stream.
    /// </summary>
    AudioClip CreateClipFromWave(string name, Stream waveStream);

    /// <summary>
    /// Creates an in-memory audio clip by loading and parsing a WAV file from disk.
    /// </summary>
    AudioClip CreateClipFromWave(string name, string filePath);

    /// <summary>
    /// Creates an audio source voice for active playback control of an <see cref="AudioClip"/>.
    /// </summary>
    AudioSource CreateSource(string name, AudioClip clip);
}
