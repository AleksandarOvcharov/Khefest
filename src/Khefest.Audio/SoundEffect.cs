namespace Khefest.Audio;

/// <summary>
/// High-level wrapper over an <see cref="AudioClip"/> providing ergonomic fire-and-forget playback.
/// </summary>
public sealed class SoundEffect : IDisposable
{
    private readonly IAudioDevice _device;
    private readonly AudioClip _clip;
    private readonly bool _ownsClip;

    public AudioClip Clip => _clip;

    public SoundEffect(IAudioDevice device, AudioClip clip, bool ownsClip = false)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(clip);

        _device = device;
        _clip = clip;
        _ownsClip = ownsClip;
    }

    /// <summary>
    /// Plays the sound effect once with default volume and pitch.
    /// </summary>
    public AudioSource Play()
    {
        return Play(1.0f, 0.0f, 0.0f);
    }

    /// <summary>
    /// Plays the sound effect once with specified volume, pitch adjustment, and stereo pan.
    /// </summary>
    public AudioSource Play(float volume, float pitch = 0.0f, float pan = 0.0f)
    {
        var source = _device.CreateSource($"Sfx_{_clip.Name}", _clip);
        source.Volume = volume;
        source.Pitch = pitch;
        source.Pan = pan;
        source.IsLooping = false;
        source.Play();
        return source;
    }

    /// <summary>
    /// Convenience static method to play an <see cref="AudioClip"/> directly as a one-shot sound effect.
    /// </summary>
    public static AudioSource Play(IAudioDevice device, AudioClip clip, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(clip);

        var sfx = new SoundEffect(device, clip, ownsClip: false);
        return sfx.Play(volume, pitch, pan);
    }

    public void Dispose()
    {
        if (_ownsClip)
        {
            _clip.Dispose();
        }
    }
}
