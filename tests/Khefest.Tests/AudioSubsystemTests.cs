using Khefest.Audio;
using Khefest.Audio.Wave;
using Khefest.Core.Resources;
using Xunit;

namespace Khefest.Tests;

public sealed class AudioSubsystemTests
{
    /// <summary>
    /// Helper to generate a minimal synthetic PCM WAV file in memory.
    /// </summary>
    private static byte[] CreateSyntheticWav(int sampleRate, short channels, short bitsPerSample, int sampleFrames)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        int bytesPerSample = bitsPerSample / 8;
        int blockAlign = channels * bytesPerSample;
        int subchunk2Size = sampleFrames * blockAlign;
        int chunkSize = 36 + subchunk2Size;

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(chunkSize);
        writer.Write("WAVE"u8);

        // 'fmt ' chunk
        writer.Write("fmt "u8);
        writer.Write(16); // Subchunk1Size
        writer.Write((short)1); // AudioFormat: PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign); // ByteRate
        writer.Write((short)blockAlign);
        writer.Write(bitsPerSample);

        // 'data' chunk
        writer.Write("data"u8);
        writer.Write(subchunk2Size);

        // Write simple sine or dummy samples
        for (int i = 0; i < sampleFrames; i++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                if (bitsPerSample == 8)
                {
                    writer.Write((byte)128);
                }
                else if (bitsPerSample == 16)
                {
                    short sample = (short)(Math.Sin(2.0 * Math.PI * 440.0 * i / sampleRate) * 16000.0);
                    writer.Write(sample);
                }
                else if (bitsPerSample == 24)
                {
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                }
                else if (bitsPerSample == 32)
                {
                    writer.Write(0);
                }
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    [Fact]
    public void WavParser_ParsesValid16BitStereoWavAccurately()
    {
        int sampleRate = 44100;
        short channels = 2;
        short bitsPerSample = 16;
        int frames = 4410; // 0.1 seconds

        byte[] wavBytes = CreateSyntheticWav(sampleRate, channels, bitsPerSample, frames);

        var waveData = WavParser.Parse(wavBytes);

        Assert.NotNull(waveData);
        Assert.Equal(44100, waveData.SampleRate);
        Assert.Equal(2, waveData.Channels);
        Assert.Equal(AudioSampleFormat.Pcm16, waveData.SampleFormat);
        Assert.Equal(16, waveData.BitsPerSample);
        Assert.Equal(frames, waveData.SampleCount);
        Assert.Equal(0.1, waveData.Duration.TotalSeconds, precision: 2);
        Assert.Equal(frames * channels * 2, waveData.RawBytes.Length);
    }

    [Fact]
    public void WavParser_ThrowsOnInvalidHeader()
    {
        byte[] corrupted = "NOT_A_VALID_RIFF_HEADER_12345678"u8.ToArray();

        Assert.Throws<InvalidDataException>(() => WavParser.Parse(corrupted));
    }

    [Fact]
    public void AudioClip_FromWaveData_CalculatesPropertiesCorrectly()
    {
        var resources = new ResourceManager(new Khefest.Core.Configuration.MemoryConfig { EnableLeakTracking = true });
        byte[] wavBytes = CreateSyntheticWav(48000, 1, 16, 2400); // 0.05 seconds mono

        var waveData = WavParser.Parse(wavBytes);
        using var clip = AudioClip.FromWaveData("LaserShot", waveData, resources);

        Assert.Equal("LaserShot", clip.Name);
        Assert.Equal(48000, clip.SampleRate);
        Assert.Equal(1, clip.Channels);
        Assert.Equal(AudioSampleFormat.Pcm16, clip.SampleFormat);
        Assert.Equal(2400, clip.SampleCount);
        Assert.False(clip.AudioData.IsEmpty);

        Assert.Single(resources.Tracker.GetActiveResources());

        clip.Dispose();

        Assert.Empty(resources.Tracker.CheckForLeaks());
        resources.Dispose();
    }

    [Fact]
    public void NullAudioDevice_ManagesLifecycleAndStateTransitionsAccurately()
    {
        var resources = new ResourceManager(new Khefest.Core.Configuration.MemoryConfig { EnableLeakTracking = true });
        using var audioDevice = new NullAudioDevice(resources);

        // 1. Master controls
        audioDevice.MasterVolume = 0.75f;
        Assert.Equal(0.75f, audioDevice.MasterVolume);
        audioDevice.IsMuted = true;
        Assert.True(audioDevice.IsMuted);

        // 2. Create clip
        byte[] wavBytes = CreateSyntheticWav(44100, 2, 16, 8820);
        using var clip = audioDevice.CreateClipFromWave("ThemeSong", new MemoryStream(wavBytes));

        // 3. Create source
        using var source = audioDevice.CreateSource("MusicSource", clip);
        Assert.Equal(PlaybackState.Stopped, source.PlaybackState);

        // 4. State transitions: Play -> Pause -> Play -> Stop
        source.Play();
        Assert.Equal(PlaybackState.Playing, source.PlaybackState);

        source.Pause();
        Assert.Equal(PlaybackState.Paused, source.PlaybackState);

        source.Play();
        Assert.Equal(PlaybackState.Playing, source.PlaybackState);

        source.Stop();
        Assert.Equal(PlaybackState.Stopped, source.PlaybackState);

        // 5. Parameter clamping validation
        source.Volume = 1.5f;
        Assert.Equal(1.5f, source.Volume);
        source.Volume = -0.5f;
        Assert.Equal(0.0f, source.Volume);

        source.Pitch = 2.0f;
        Assert.Equal(1.0f, source.Pitch); // Clamped to 1.0
        source.Pitch = -2.0f;
        Assert.Equal(-1.0f, source.Pitch); // Clamped to -1.0

        source.Pan = 1.8f;
        Assert.Equal(1.0f, source.Pan); // Clamped to 1.0
        source.Pan = -1.8f;
        Assert.Equal(-1.0f, source.Pan); // Clamped to -1.0

        source.IsLooping = true;
        Assert.True(source.IsLooping);

        // 6. Cleanup verification
        source.Dispose();
        clip.Dispose();

        Assert.Empty(resources.Tracker.CheckForLeaks());
        resources.Dispose();
    }

    [Fact]
    public void SoundEffect_FireAndForgetPlay_CreatesAndStartsPlayingSource()
    {
        var resources = new ResourceManager(new Khefest.Core.Configuration.MemoryConfig { EnableLeakTracking = true });
        using var audioDevice = new NullAudioDevice(resources);

        byte[] wavBytes = CreateSyntheticWav(44100, 1, 16, 1000);
        using var clip = audioDevice.CreateClipFromWave("HitSound", new MemoryStream(wavBytes));
        using var sfx = new SoundEffect(audioDevice, clip);

        using var source = sfx.Play(0.8f, 0.2f, -0.5f);

        Assert.Equal(PlaybackState.Playing, source.PlaybackState);
        Assert.Equal(0.8f, source.Volume);
        Assert.Equal(0.2f, source.Pitch);
        Assert.Equal(-0.5f, source.Pan);
        Assert.False(source.IsLooping);

        source.Dispose();
        clip.Dispose();

        Assert.Empty(resources.Tracker.CheckForLeaks());
        resources.Dispose();
    }

    [Fact]
    public void WindowsAudioDevice_InitializesAndPlaysSyntheticWav_WithoutExceptionsOrLeaks()
    {
        var resources = new ResourceManager(new Khefest.Core.Configuration.MemoryConfig { EnableLeakTracking = true });
        
        using var audioDevice = new Khefest.Windows.Audio.WindowsAudioDevice(resources);
        Assert.NotNull(audioDevice);
        Assert.Equal(1.0f, audioDevice.MasterVolume);
        Assert.False(audioDevice.IsMuted);

        // 1. Create 44.1kHz Stereo 16-bit audio clip (0.1s)
        byte[] wavBytes = CreateSyntheticWav(44100, 2, 16, 4410);
        using var clip = audioDevice.CreateClipFromWave("WindowsTestTone", new MemoryStream(wavBytes));
        Assert.Equal(44100, clip.SampleRate);
        Assert.Equal(2, clip.Channels);

        // 2. Create audio source
        using var source = audioDevice.CreateSource("WindowsTestSource", clip);
        Assert.Equal(PlaybackState.Stopped, source.PlaybackState);

        // 3. Play, test volume, pitch, pan
        source.Volume = 0.5f;
        source.Pitch = 0.5f;
        source.Pan = -0.5f;
        source.Play();
        Assert.Equal(PlaybackState.Playing, source.PlaybackState);

        // 4. Pause and resume
        source.Pause();
        Assert.Equal(PlaybackState.Paused, source.PlaybackState);

        source.Play();
        Assert.Equal(PlaybackState.Playing, source.PlaybackState);

        // 5. Stop
        source.Stop();
        Assert.Equal(PlaybackState.Stopped, source.PlaybackState);

        // 6. Test multiple simultaneous sources
        using var source2 = audioDevice.CreateSource("WindowsTestSource2", clip);
        source2.Volume = 0.8f;
        source2.Play();
        source.Play();

        source.Stop();
        source2.Stop();

        // 7. Cleanup & leak check
        source2.Dispose();
        source.Dispose();
        clip.Dispose();

        Assert.Empty(resources.Tracker.CheckForLeaks());
        resources.Dispose();
    }



    [Fact]
    public void WavAssetLoader_LoadsAudioClip_ViaAssetManager()
    {
        var resources = new ResourceManager(new Khefest.Core.Configuration.MemoryConfig { EnableLeakTracking = true });
        using var audioDevice = new NullAudioDevice(resources);

        string tempDir = Path.Combine(Path.GetTempPath(), $"khefest_audio_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string wavPath = Path.Combine(tempDir, "explosion.wav");
            byte[] wavBytes = CreateSyntheticWav(22050, 1, 16, 2205);
            File.WriteAllBytes(wavPath, wavBytes);

            var assetManager = new Khefest.Core.Assets.AssetManager(tempDir, resources);
            assetManager.RegisterLoader(new Khefest.Audio.Assets.WavAssetLoader(audioDevice));

            var clip = assetManager.Load<AudioClip>("explosion.wav");
            Assert.NotNull(clip);
            Assert.Equal("explosion", clip.Name);
            Assert.Equal(22050, clip.SampleRate);
            Assert.Equal(1, clip.Channels);

            assetManager.Unload("explosion.wav");
            assetManager.Dispose();

            Assert.Empty(resources.Tracker.CheckForLeaks());
        }
        finally
        {
            Directory.Delete(tempDir, true);
            resources.Dispose();
        }
    }
}
