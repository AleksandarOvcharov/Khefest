namespace Khefest.Audio;

/// <summary>
/// Specifies the playback state of an audio source.
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// Playback is completely stopped and rewinded to the start.
    /// </summary>
    Stopped,

    /// <summary>
    /// Currently actively playing audio.
    /// </summary>
    Playing,

    /// <summary>
    /// Playback is temporarily suspended at the current position.
    /// </summary>
    Paused
}
