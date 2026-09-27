using System.Numerics;
using Khefest.Audio;
using Khefest.Audio.Wave;
using Khefest.Core.Configuration;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.HelloAudio;

/// <summary>
/// Simple audio showcase demonstrating sound clips, sound effects,
/// dynamic volume, pitch, pan, looping, and visual feedback.
/// </summary>
public sealed class HelloAudioGame : Game
{
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;

    private AudioClip? _synthToneClip;
    private AudioClip? _musicClip;
    private AudioSource? _loopingMusicSource;

    private float _pan = 0.0f;
    private float _pitch = 0.0f;
    private float _volume = 0.7f;
    private string _lastAction = "Press Space to play sound effect!";

    public override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GpuDevice);
        _camera = new Camera2D(Window.Width, Window.Height);

        // 1. Create short synth beep clip (440 Hz, 0.25s) for sound effects
        byte[] beepWav = GenerateToneWav(frequency: 440f, durationSeconds: 0.25f);
        using (var beepStream = new MemoryStream(beepWav))
        {
            _synthToneClip = Audio.CreateClipFromWave("BeepTone", beepStream);
        }

        // 2. Create ambient melodic chord clip (2.0s) for looping background music
        byte[] musicWav = GenerateChordWav(durationSeconds: 2.0f);
        using (var musicStream = new MemoryStream(musicWav))
        {
            _musicClip = Audio.CreateClipFromWave("AmbientChord", musicStream);
        }

        // 3. Create a looping background audio source
        _loopingMusicSource = Audio.CreateSource("MusicSource", _musicClip);
        _loopingMusicSource.IsLooping = true;
        _loopingMusicSource.Volume = _volume;
        _loopingMusicSource.Play();
    }

    public override void OnResize(int width, int height)
    {
        _camera?.Resize(width, height);
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.IsKeyDown(Key.Escape))
            Exit();

        float dt = gameTime.DeltaTime;

        // Space: Trigger sound effect
        if (Input.IsKeyPressed(Key.Space) && _synthToneClip != null)
        {
            SoundEffect.Play(Audio, _synthToneClip, volume: _volume, pitch: _pitch, pan: _pan);
            _lastAction = "Triggered sound effect via SoundEffect.Play!";
        }

        // M: Toggle background music Pause / Play
        if (Input.IsKeyPressed(Key.M) && _loopingMusicSource != null)
        {
            if (_loopingMusicSource.PlaybackState == PlaybackState.Playing)
            {
                _loopingMusicSource.Pause();
                _lastAction = "Paused background music.";
            }
            else
            {
                _loopingMusicSource.Play();
                _lastAction = "Resumed background music.";
            }
        }

        // Up/Down: Adjust pitch of music and sound effect
        if (Input.IsKeyDown(Key.Up))
        {
            _pitch = Math.Clamp(_pitch + dt * 1.5f, -1.0f, 1.0f);
            if (_loopingMusicSource != null) _loopingMusicSource.Pitch = _pitch;
            _lastAction = $"Pitch increased: {_pitch:+0.00;-0.00}";
        }
        if (Input.IsKeyDown(Key.Down))
        {
            _pitch = Math.Clamp(_pitch - dt * 1.5f, -1.0f, 1.0f);
            if (_loopingMusicSource != null) _loopingMusicSource.Pitch = _pitch;
            _lastAction = $"Pitch decreased: {_pitch:+0.00;-0.00}";
        }

        // Left/Right: Adjust stereo pan
        if (Input.IsKeyDown(Key.Left))
        {
            _pan = Math.Clamp(_pan - dt * 1.5f, -1.0f, 1.0f);
            if (_loopingMusicSource != null) _loopingMusicSource.Pan = _pan;
            _lastAction = $"Pan left: {_pan:+0.00;-0.00}";
        }
        if (Input.IsKeyDown(Key.Right))
        {
            _pan = Math.Clamp(_pan + dt * 1.5f, -1.0f, 1.0f);
            if (_loopingMusicSource != null) _loopingMusicSource.Pan = _pan;
            _lastAction = $"Pan right: {_pan:+0.00;-0.00}";
        }

        // W/S: Adjust volume
        if (Input.IsKeyDown(Key.W))
        {
            _volume = Math.Clamp(_volume + dt * 0.8f, 0.0f, 1.0f);
            if (_loopingMusicSource != null) _loopingMusicSource.Volume = _volume;
            _lastAction = $"Volume up: {_volume:P0}";
        }
        if (Input.IsKeyDown(Key.S))
        {
            _volume = Math.Clamp(_volume - dt * 0.8f, 0.0f, 1.0f);
            if (_loopingMusicSource != null) _loopingMusicSource.Volume = _volume;
            _lastAction = $"Volume down: {_volume:P0}";
        }
    }

    public override void Render(GameTime gameTime)
    {
        var backBuffer = SwapChain.CurrentBackBuffer;

        // 1. Scoped clear pass
        var clearPass = new RenderPassDesc
        {
            ColorTargets = [backBuffer],
            ClearColor = new Color4(0.08f, 0.10f, 0.14f, 1.0f),
            ClearColorTarget = true
        };

        using (var cmd = GpuDevice.CreateCommandRecorder())
        {
            using (cmd.BeginScopedPass(clearPass))
            {
                // Scoped clear
            }
            GpuDevice.Submit(cmd);
        }

        // 2. High-level visual audio representation
        _spriteBatch?.Begin(backBuffer, camera: null);

        // Header & info
        _spriteBatch?.DrawString("Khefest Audio Engine Showcase", new Vector2(30, 30), Color4.Gold, scale: 1.4f);
        _spriteBatch?.DrawString($"Status: {_lastAction}", new Vector2(30, 75), Color4.Cyan, scale: 1.05f);

        // Control hints
        _spriteBatch?.DrawString("Controls:", new Vector2(30, 120), Color4.White, scale: 1.1f);
        _spriteBatch?.DrawString("  [SPACE]       - Play Sound Effect (Beep)", new Vector2(30, 150), Color4.White);
        _spriteBatch?.DrawString("  [M]           - Pause / Resume Background Music", new Vector2(30, 175), Color4.White);
        _spriteBatch?.DrawString("  [W / S]       - Volume Up / Down", new Vector2(30, 200), Color4.White);
        _spriteBatch?.DrawString("  [UP / DOWN]   - Pitch Up / Down (-1.0 to +1.0)", new Vector2(30, 225), Color4.White);
        _spriteBatch?.DrawString("  [LEFT / RIGHT]- Stereo Pan (-1.0 Left to +1.0 Right)", new Vector2(30, 250), Color4.White);
        _spriteBatch?.DrawString("  [ESC]         - Exit", new Vector2(30, 275), Color4.White);

        // Visual meters
        // Volume meter
        _spriteBatch?.DrawString($"Master Volume: {_volume * 100:F0}%", new Vector2(30, 330), Color4.White);
        _spriteBatch?.DrawRectangle(new Vector2(200, 330), new Vector2(250, 20), Color4.DarkGray);
        _spriteBatch?.FillRectangle(new Vector2(200, 330), new Vector2(250 * _volume, 20), Color4.LimeGreen);

        // Pitch meter
        _spriteBatch?.DrawString($"Pitch Shift: {_pitch:+0.00;-0.00}", new Vector2(30, 370), Color4.White);
        _spriteBatch?.DrawRectangle(new Vector2(200, 370), new Vector2(250, 20), Color4.DarkGray);
        float pitchNorm = (_pitch + 1.0f) * 0.5f;
        _spriteBatch?.FillCircle(new Vector2(200 + 250 * pitchNorm, 380), 8f, Color4.Orange);

        // Stereo Pan meter
        _spriteBatch?.DrawString($"Stereo Pan:  {_pan:+0.00;-0.00}", new Vector2(30, 410), Color4.White);
        _spriteBatch?.DrawRectangle(new Vector2(200, 410), new Vector2(250, 20), Color4.DarkGray);
        float panNorm = (_pan + 1.0f) * 0.5f;
        _spriteBatch?.FillCircle(new Vector2(200 + 250 * panNorm, 420), 8f, Color4.CornflowerBlue);

        // Music state indicator
        string musicState = _loopingMusicSource?.PlaybackState.ToString() ?? "Stopped";
        Color4 musicColor = _loopingMusicSource?.PlaybackState == PlaybackState.Playing ? Color4.LimeGreen : Color4.Crimson;
        _spriteBatch?.DrawString($"Background Music State: {musicState}", new Vector2(30, 460), musicColor, scale: 1.1f);

        _spriteBatch?.End();
    }

    public override void Shutdown()
    {
        _loopingMusicSource?.Dispose();
        _musicClip?.Dispose();
        _synthToneClip?.Dispose();
        _spriteBatch?.Dispose();
        base.Shutdown();
    }

    /// <summary>
    /// Helper to generate a minimal in-memory RIFF/WAVE tone for standalone execution.
    /// </summary>
    private static byte[] GenerateToneWav(float frequency, float durationSeconds)
    {
        const int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * durationSeconds);
        short[] pcm = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1.0f - (float)i / sampleCount; // linear decay
            pcm[i] = (short)(MathF.Sin(2f * MathF.PI * frequency * t) * 16000f * envelope);
        }

        return CreateWavBytes(pcm, sampleRate, channels: 1);
    }

    /// <summary>
    /// Helper to generate a smooth ambient chord in-memory for standalone music looping.
    /// </summary>
    private static byte[] GenerateChordWav(float durationSeconds)
    {
        const int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * durationSeconds);
        short[] pcm = new short[sampleCount * 2]; // Stereo

        float[] chordFreqs = [220.0f, 277.18f, 329.63f]; // A minor chord (A3, C#4, E4)

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float wave = 0f;
            foreach (var f in chordFreqs)
            {
                wave += MathF.Sin(2f * MathF.PI * f * t);
            }
            wave /= chordFreqs.Length;

            // Smooth cosine loop fade at start and end
            float loopEnvelope = MathF.Sin(MathF.PI * (float)i / sampleCount);
            short sample = (short)(wave * 12000f * loopEnvelope);

            pcm[i * 2] = sample;     // Left
            pcm[i * 2 + 1] = sample; // Right
        }

        return CreateWavBytes(pcm, sampleRate, channels: 2);
    }

    private static byte[] CreateWavBytes(short[] samples, int sampleRate, short channels)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        int bytesPerSample = 2;
        int blockAlign = channels * bytesPerSample;
        int subchunk2Size = samples.Length * bytesPerSample;
        int chunkSize = 36 + subchunk2Size;

        writer.Write("RIFF"u8);
        writer.Write(chunkSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((short)blockAlign);
        writer.Write((short)16); // 16 bits

        writer.Write("data"u8);
        writer.Write(subchunk2Size);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        return stream.ToArray();
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "Khefest - HelloAudio Example",
                Width = 1024,
                Height = 600,
                VSync = true
            })
            .Build();

        var game = new HelloAudioGame();
        KhefestApp.Run(game, config);
    }
}
