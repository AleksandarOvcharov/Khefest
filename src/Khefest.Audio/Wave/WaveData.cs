namespace Khefest.Audio.Wave;

/// <summary>
/// Contains raw decoded PCM audio data parsed from a WAV waveform container.
/// </summary>
public sealed class WaveData
{
    public int SampleRate { get; }
    public int Channels { get; }
    public AudioSampleFormat SampleFormat { get; }
    public int BitsPerSample { get; }
    public byte[] RawBytes { get; }
    public int SampleCount { get; }
    public TimeSpan Duration { get; }

    public WaveData(
        int sampleRate,
        int channels,
        AudioSampleFormat sampleFormat,
        int bitsPerSample,
        byte[] rawBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentNullException.ThrowIfNull(rawBytes);

        SampleRate = sampleRate;
        Channels = channels;
        SampleFormat = sampleFormat;
        BitsPerSample = bitsPerSample;
        RawBytes = rawBytes;

        int bytesPerFrame = channels * (bitsPerSample / 8);
        SampleCount = bytesPerFrame > 0 ? rawBytes.Length / bytesPerFrame : 0;
        Duration = TimeSpan.FromSeconds(SampleRate > 0 ? (double)SampleCount / SampleRate : 0.0);
    }
}
