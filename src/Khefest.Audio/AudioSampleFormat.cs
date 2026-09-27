namespace Khefest.Audio;

/// <summary>
/// Specifies the audio sample encoding format.
/// </summary>
public enum AudioSampleFormat
{
    /// <summary>
    /// 8-bit unsigned integer PCM.
    /// </summary>
    Pcm8 = 1,

    /// <summary>
    /// 16-bit signed integer linear PCM.
    /// </summary>
    Pcm16 = 2,

    /// <summary>
    /// 24-bit signed integer linear PCM.
    /// </summary>
    Pcm24 = 3,

    /// <summary>
    /// 32-bit signed integer linear PCM.
    /// </summary>
    Pcm32 = 4,

    /// <summary>
    /// 32-bit IEEE single-precision floating point PCM.
    /// </summary>
    IeeeFloat32 = 5
}
