using Khefest.Audio.Wave;
using Khefest.Core.Resources;

namespace Khefest.Audio;

/// <summary>
/// A headless, null-operation audio device used for testing, non-audio environments, or fallback when no audio hardware is present.
/// </summary>
public sealed class NullAudioDevice : IAudioDevice
{
    private readonly ResourceManager? _manager;
    private float _masterVolume = 1.0f;
    private bool _isMuted;

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Max(0.0f, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public NullAudioDevice(ResourceManager? manager = null)
    {
        _manager = manager;
    }

    public AudioClip CreateClip(
        string name,
        int sampleRate,
        int channels,
        AudioSampleFormat sampleFormat,
        int bitsPerSample,
        byte[] rawAudioData)
    {
        return new AudioClip(name, sampleRate, channels, sampleFormat, bitsPerSample, rawAudioData, _manager);
    }

    public AudioClip CreateClipFromWave(string name, Stream waveStream)
    {
        var waveData = WavParser.Parse(waveStream);
        return AudioClip.FromWaveData(name, waveData, _manager);
    }

    public AudioClip CreateClipFromWave(string name, string filePath)
    {
        var waveData = WavParser.Parse(filePath);
        return AudioClip.FromWaveData(name, waveData, _manager);
    }

    public AudioSource CreateSource(string name, AudioClip clip)
    {
        return new NullAudioSource(name, clip, _manager);
    }

    public void Dispose()
    {
    }

    private sealed class NullAudioSource : AudioSource
    {
        private PlaybackState _playbackState = PlaybackState.Stopped;

        public override PlaybackState PlaybackState => _playbackState;

        public NullAudioSource(string name, AudioClip clip, ResourceManager? manager)
            : base(name, clip, manager)
        {
        }

        public override void Play()
        {
            _playbackState = PlaybackState.Playing;
        }

        public override void Pause()
        {
            if (_playbackState == PlaybackState.Playing)
            {
                _playbackState = PlaybackState.Paused;
            }
        }

        public override void Stop()
        {
            _playbackState = PlaybackState.Stopped;
        }
    }
}
