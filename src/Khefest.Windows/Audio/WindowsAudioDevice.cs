using System.Runtime.InteropServices;
using Khefest.Audio;
using Khefest.Audio.Wave;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;

namespace Khefest.Windows.Audio;

/// <summary>
/// Hardware audio device for Windows backed natively by DirectSound / XAudio 2.9.
/// </summary>
public sealed unsafe class WindowsAudioDevice : IAudioDevice
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Audio, "WindowsAudioDevice");

    private readonly ResourceManager? _manager;
    private nint _xaudio2;
    private nint _masteringVoice;
    private float _masterVolume = 1.0f;
    private bool _isMuted;
    private int _disposed;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Max(0.0f, value);
            ApplyMasterVolume();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyMasterVolume();
        }
    }

    public WindowsAudioDevice(ResourceManager? manager = null)
    {
        _manager = manager;

        int hr = XAudio2Native.XAudio2Create(out _xaudio2, 0, XAudio2Native.XAUDIO2_DEFAULT_PROCESSOR);
        if (hr < 0 || _xaudio2 == nint.Zero)
        {
            throw new InvalidOperationException($"Failed to initialize XAudio2 engine. HRESULT: 0x{hr:X8}");
        }

        hr = XAudio2Native.CreateMasteringVoice(_xaudio2, out _masteringVoice);
        if (hr < 0 || _masteringVoice == nint.Zero)
        {
            Marshal.Release(_xaudio2);
            _xaudio2 = nint.Zero;
            throw new InvalidOperationException($"Failed to create XAudio2 mastering voice. HRESULT: 0x{hr:X8}");
        }

        ApplyMasterVolume();
        Logger.Info("WindowsAudioDevice initialized successfully via XAudio2 2.9.");
    }

    private void ApplyMasterVolume()
    {
        if (_masteringVoice != nint.Zero)
        {
            float targetVolume = _isMuted ? 0.0f : _masterVolume;
            XAudio2Native.SetVolume(_masteringVoice, targetVolume);
        }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clip);

        ThrowIfDisposed();

        var waveFormat = new XAudio2Native.WAVEFORMATEX
        {
            wFormatTag = clip.SampleFormat == AudioSampleFormat.IeeeFloat32 ? XAudio2Native.WAVE_FORMAT_IEEE_FLOAT : XAudio2Native.WAVE_FORMAT_PCM,
            nChannels = (ushort)clip.Channels,
            nSamplesPerSec = (uint)clip.SampleRate,
            wBitsPerSample = (ushort)clip.BitsPerSample,
            nBlockAlign = (ushort)(clip.Channels * (clip.BitsPerSample / 8)),
            nAvgBytesPerSec = (uint)(clip.SampleRate * clip.Channels * (clip.BitsPerSample / 8)),
            cbSize = 0
        };

        nint sourceVoice;
        int hr = XAudio2Native.CreateSourceVoice(_xaudio2, out sourceVoice, &waveFormat, 0, 4.0f);
        if (hr < 0 || sourceVoice == nint.Zero)
        {
            throw new InvalidOperationException($"Failed to create XAudio2 source voice for clip '{clip.Name}'. HRESULT: 0x{hr:X8}");
        }

        var source = new WindowsAudioSource(name, clip, sourceVoice, _manager);
        return source;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(WindowsAudioDevice));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_masteringVoice != nint.Zero)
            {
                XAudio2Native.DestroyVoice(_masteringVoice);
                _masteringVoice = nint.Zero;
            }

            if (_xaudio2 != nint.Zero)
            {
                Marshal.Release(_xaudio2);
                _xaudio2 = nint.Zero;
            }

            Logger.Info("WindowsAudioDevice terminated and freed XAudio2 resources.");
        }
    }

    private sealed class WindowsAudioSource : AudioSource
    {
        private readonly nint _sourceVoice;
        private GCHandle _pinHandle;
        private PlaybackState _state = PlaybackState.Stopped;
        private bool _submitted;

        public override PlaybackState PlaybackState
        {
            get
            {
                if (_state == PlaybackState.Playing)
                {
                    XAudio2Native.GetSourceVoiceState(_sourceVoice, out var voiceState);
                    if (voiceState.BuffersQueued == 0)
                    {
                        _state = PlaybackState.Stopped;
                        _submitted = false;
                    }
                }
                return _state;
            }
        }

        public WindowsAudioSource(string name, AudioClip clip, nint sourceVoice, ResourceManager? manager)
            : base(name, clip, manager)
        {
            _sourceVoice = sourceVoice;

            // Pin underlying audio byte array so XAudio2 can read it reliably
            var rawBytes = clip.AudioData.ToArray();
            _pinHandle = GCHandle.Alloc(rawBytes, GCHandleType.Pinned);

            ApplyVolume();
            ApplyPitch();
            ApplyPan();
        }

        public override void Play()
        {
            ThrowIfDisposed();

            if (_state == PlaybackState.Paused)
            {
                XAudio2Native.StartSourceVoice(_sourceVoice);
                _state = PlaybackState.Playing;
                return;
            }

            if (_state == PlaybackState.Playing)
            {
                Stop();
            }

            // Ensure buffer is queued
            SubmitBuffer();

            XAudio2Native.StartSourceVoice(_sourceVoice);
            _state = PlaybackState.Playing;
        }

        public override void Pause()
        {
            ThrowIfDisposed();

            if (PlaybackState == PlaybackState.Playing)
            {
                XAudio2Native.StopSourceVoice(_sourceVoice);
                _state = PlaybackState.Paused;
            }
        }

        public override void Stop()
        {
            ThrowIfDisposed();

            XAudio2Native.StopSourceVoice(_sourceVoice);
            XAudio2Native.FlushSourceBuffers(_sourceVoice);
            _submitted = false;
            _state = PlaybackState.Stopped;
        }

        private void SubmitBuffer()
        {
            if (!_submitted)
            {
                var pAudio = (byte*)_pinHandle.AddrOfPinnedObject();
                var buffer = new XAudio2Native.XAUDIO2_BUFFER
                {
                    Flags = 0x0040, // XAUDIO2_END_OF_STREAM
                    AudioBytes = (uint)Clip.AudioData.Length,
                    pAudioData = pAudio,
                    PlayBegin = 0,
                    PlayLength = 0,
                    LoopBegin = 0,
                    LoopLength = 0,
                    LoopCount = IsLooping ? XAudio2Native.XAUDIO2_LOOP_INFINITE : 0,
                    pContext = null
                };

                XAudio2Native.SubmitSourceBuffer(_sourceVoice, &buffer);
                _submitted = true;
            }
        }

        protected override void OnVolumeChanged(float volume)
        {
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (_sourceVoice != nint.Zero)
            {
                XAudio2Native.SetVolume(_sourceVoice, Volume);
            }
        }

        protected override void OnPitchChanged(float pitch)
        {
            ApplyPitch();
        }

        private void ApplyPitch()
        {
            if (_sourceVoice != nint.Zero)
            {
                // pitch is in [-1.0, 1.0], map to frequency ratio 2^pitch [0.5, 2.0]
                float ratio = MathF.Pow(2.0f, Pitch);
                XAudio2Native.SetFrequencyRatio(_sourceVoice, ratio);
            }
        }

        protected override void OnPanChanged(float pan)
        {
            ApplyPan();
        }

        private void ApplyPan()
        {
            if (_sourceVoice != nint.Zero)
            {
                // Standard stereo panning matrix [left, right]
                float left = MathF.Cos((Pan + 1.0f) * MathF.PI / 4.0f);
                float right = MathF.Sin((Pan + 1.0f) * MathF.PI / 4.0f);

                if (Clip.Channels == 1)
                {
                    // Mono source -> Stereo output (1 source ch, 2 dest ch)
                    float* matrix = stackalloc float[2];
                    matrix[0] = left;
                    matrix[1] = right;
                    XAudio2Native.SetOutputMatrix(_sourceVoice, nint.Zero, 1, 2, matrix);
                }
                else if (Clip.Channels == 2)
                {
                    // Stereo source -> Stereo output (2 source ch, 2 dest ch)
                    // Matrix: [LeftIn->LeftOut, LeftIn->RightOut, RightIn->LeftOut, RightIn->RightOut]
                    float* matrix = stackalloc float[4];
                    matrix[0] = left;  // LeftIn -> LeftOut
                    matrix[1] = 0.0f;  // LeftIn -> RightOut
                    matrix[2] = 0.0f;  // RightIn -> LeftOut
                    matrix[3] = right; // RightIn -> RightOut
                    XAudio2Native.SetOutputMatrix(_sourceVoice, nint.Zero, 2, 2, matrix);
                }
            }
        }

        protected override void OnLoopingChanged(bool looping)
        {
            if (PlaybackState == PlaybackState.Playing)
            {
                // If currently playing, restart with updated looping flag
                Play();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sourceVoice != nint.Zero)
                {
                    XAudio2Native.StopSourceVoice(_sourceVoice);
                    XAudio2Native.FlushSourceBuffers(_sourceVoice);
                    XAudio2Native.DestroyVoice(_sourceVoice);
                }

                if (_pinHandle.IsAllocated)
                {
                    _pinHandle.Free();
                }

                _state = PlaybackState.Stopped;
                _submitted = false;
            }

            base.Dispose(disposing);
        }
    }
}
