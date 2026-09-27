using Khefest.Core.Resources;

namespace Khefest.Audio;

/// <summary>
/// Controls the active playback, volume, pitch, pan, and looping of an <see cref="AudioClip"/>.
/// </summary>
public abstract class AudioSource : ResourceBase
{
    private float _volume = 1.0f;
    private float _pitch = 0.0f;
    private float _pan = 0.0f;
    private bool _isLooping;
    private readonly ResourceManager? _manager;

    public AudioClip Clip { get; }

    /// <summary>
    /// Gets or sets the volume multiplier in range [0.0, 1.0+]. Default is 1.0.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            float clamped = Math.Max(0.0f, value);
            if (Math.Abs(_volume - clamped) > float.Epsilon)
            {
                _volume = clamped;
                OnVolumeChanged(_volume);
            }
        }
    }

    /// <summary>
    /// Gets or sets the pitch adjustment in semitones or octaves (typically in range [-1.0, 1.0], where 0.0 is normal pitch).
    /// </summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            float clamped = Math.Clamp(value, -1.0f, 1.0f);
            if (Math.Abs(_pitch - clamped) > float.Epsilon)
            {
                _pitch = clamped;
                OnPitchChanged(_pitch);
            }
        }
    }

    /// <summary>
    /// Gets or sets the stereo panning balance in range [-1.0 (full left), 1.0 (full right)]. Default is 0.0 (center).
    /// </summary>
    public float Pan
    {
        get => _pan;
        set
        {
            float clamped = Math.Clamp(value, -1.0f, 1.0f);
            if (Math.Abs(_pan - clamped) > float.Epsilon)
            {
                _pan = clamped;
                OnPanChanged(_pan);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this audio source loops indefinitely when playback reaches the end.
    /// </summary>
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (_isLooping != value)
            {
                _isLooping = value;
                OnLoopingChanged(_isLooping);
            }
        }
    }

    /// <summary>
    /// Gets the current playback state of the source.
    /// </summary>
    public abstract PlaybackState PlaybackState { get; }

    protected AudioSource(string name, AudioClip clip, ResourceManager? manager = null)
        : base(name, ResourceType.Audio, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clip);

        Clip = clip;
        _manager = manager;

        _manager?.Tracker.Track(this);
    }

    /// <summary>
    /// Starts or resumes playback of the audio clip.
    /// </summary>
    public abstract void Play();

    /// <summary>
    /// Pauses playback at the current playback position.
    /// </summary>
    public abstract void Pause();

    /// <summary>
    /// Stops playback and rewinds to the beginning of the clip.
    /// </summary>
    public abstract void Stop();

    protected virtual void OnVolumeChanged(float volume) { }
    protected virtual void OnPitchChanged(float pitch) { }
    protected virtual void OnPanChanged(float pan) { }
    protected virtual void OnLoopingChanged(bool looping) { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager?.Tracker.Untrack(Id);
        }
    }
}
