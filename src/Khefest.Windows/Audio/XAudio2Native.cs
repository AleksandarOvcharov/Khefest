using System.Runtime.InteropServices;

namespace Khefest.Windows.Audio;

/// <summary>
/// Minimal native interop definitions for DirectSound / XAudio 2.9 (xaudio2_9.dll).
/// Native COM pointers and vtables are strictly internal to Khefest.Windows.
/// </summary>
internal static unsafe class XAudio2Native
{
    private const string XAudio2Dll = "xaudio2_9.dll";

    public const uint XAUDIO2_DEFAULT_PROCESSOR = 0x00000001;
    public const uint XAUDIO2_COMMIT_NOW = 0;
    public const uint XAUDIO2_LOOP_INFINITE = 255;
    public const ushort WAVE_FORMAT_PCM = 1;
    public const ushort WAVE_FORMAT_IEEE_FLOAT = 3;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XAUDIO2_BUFFER
    {
        public uint Flags;
        public uint AudioBytes;
        public byte* pAudioData;
        public uint PlayBegin;
        public uint PlayLength;
        public uint LoopBegin;
        public uint LoopLength;
        public uint LoopCount;
        public void* pContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XAUDIO2_VOICE_STATE
    {
        public void* pCurrentBufferContext;
        public uint BuffersQueued;
        public ulong SamplesPlayed;
    }

    [DllImport(XAudio2Dll, EntryPoint = "XAudio2Create", ExactSpelling = true)]
    public static extern int XAudio2Create(out nint ppXAudio2, uint Flags, uint XAudio2Processor);

    // IXAudio2 vtable offsets (inherits IUnknown: 0=QI, 1=AddRef, 2=Release):
    // 3: RegisterForCallbacks
    // 4: UnregisterForCallbacks
    // 5: CreateSourceVoice
    // 6: CreateSubmixVoice
    // 7: CreateMasteringVoice
    // 8: StartEngine
    // 9: StopEngine
    // 10: CommitChanges
    // 11: GetPerformanceData
    // 12: SetDebugConfiguration

    public static int CreateSourceVoice(
        nint xaudio2,
        out nint ppSourceVoice,
        WAVEFORMATEX* pSourceFormat,
        uint flags = 0,
        float maxFrequencyRatio = 2.0f,
        void* pCallback = null,
        void* pSendList = null,
        void* pEffectChain = null)
    {
        var vtbl = *(void***)xaudio2;
        var func = (delegate* unmanaged[Stdcall]<nint, out nint, WAVEFORMATEX*, uint, float, void*, void*, void*, int>)vtbl[5];
        return func(xaudio2, out ppSourceVoice, pSourceFormat, flags, maxFrequencyRatio, pCallback, pSendList, pEffectChain);
    }

    public static int CreateMasteringVoice(
        nint xaudio2,
        out nint ppMasteringVoice,
        uint inputChannels = 0,
        uint inputSampleRate = 0,
        uint flags = 0,
        char* szDeviceId = null,
        void* pEffectChain = null,
        int streamCategory = 0)
    {
        var vtbl = *(void***)xaudio2;
        var func = (delegate* unmanaged[Stdcall]<nint, out nint, uint, uint, uint, char*, void*, int, int>)vtbl[7];
        return func(xaudio2, out ppMasteringVoice, inputChannels, inputSampleRate, flags, szDeviceId, pEffectChain, streamCategory);
    }

    // IXAudio2Voice does NOT inherit IUnknown in C++ (it's DECLARE_INTERFACE(IXAudio2Voice))
    // Vtable offsets:
    // 0: GetVoiceDetails
    // 1: SetOutputVoices
    // 2: SetEffectChain
    // 3: EnableEffect
    // 4: DisableEffect
    // 5: GetEffectState
    // 6: SetEffectParameters
    // 7: GetEffectParameters
    // 8: SetFilterParameters
    // 9: GetFilterParameters
    // 10: SetOutputFilterParameters
    // 11: GetOutputFilterParameters
    // 12: SetVolume
    // 13: GetVolume
    // 14: SetChannelVolumes
    // 15: GetChannelVolumes
    // 16: SetOutputMatrix
    // 17: GetOutputMatrix
    // 18: DestroyVoice

    public static void DestroyVoice(nint voice)
    {
        if (voice == nint.Zero) return;
        var vtbl = *(void***)voice;
        var func = (delegate* unmanaged[Stdcall]<nint, void>)vtbl[18];
        func(voice);
    }

    public static int SetVolume(nint voice, float volume, uint operationSet = XAUDIO2_COMMIT_NOW)
    {
        var vtbl = *(void***)voice;
        var func = (delegate* unmanaged[Stdcall]<nint, float, uint, int>)vtbl[12];
        return func(voice, volume, operationSet);
    }

    public static int SetOutputMatrix(
        nint voice,
        nint pDestinationVoice,
        uint sourceChannels,
        uint destinationChannels,
        float* pLevelMatrix,
        uint operationSet = XAUDIO2_COMMIT_NOW)
    {
        var vtbl = *(void***)voice;
        var func = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, float*, uint, int>)vtbl[16];
        return func(voice, pDestinationVoice, sourceChannels, destinationChannels, pLevelMatrix, operationSet);
    }

    // IXAudio2SourceVoice vtable offsets (extends IXAudio2Voice, so index starts at 19):
    // 19: Start
    // 20: Stop
    // 21: SubmitSourceBuffer
    // 22: FlushSourceBuffers
    // 23: Discontinuity
    // 24: ExitLoop
    // 25: GetState
    // 26: SetFrequencyRatio
    // 27: GetFrequencyRatio
    // 28: SetSourceSampleRate

    public static int StartSourceVoice(nint sourceVoice, uint flags = 0, uint operationSet = XAUDIO2_COMMIT_NOW)
    {
        var vtbl = *(void***)sourceVoice;
        var func = (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)vtbl[19];
        return func(sourceVoice, flags, operationSet);
    }

    public static int StopSourceVoice(nint sourceVoice, uint flags = 0, uint operationSet = XAUDIO2_COMMIT_NOW)
    {
        var vtbl = *(void***)sourceVoice;
        var func = (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)vtbl[20];
        return func(sourceVoice, flags, operationSet);
    }

    public static int SubmitSourceBuffer(nint sourceVoice, XAUDIO2_BUFFER* pBuffer, void* pBufferWMA = null)
    {
        var vtbl = *(void***)sourceVoice;
        var func = (delegate* unmanaged[Stdcall]<nint, XAUDIO2_BUFFER*, void*, int>)vtbl[21];
        return func(sourceVoice, pBuffer, pBufferWMA);
    }

    public static int FlushSourceBuffers(nint sourceVoice)
    {
        var vtbl = *(void***)sourceVoice;
        var func = (delegate* unmanaged[Stdcall]<nint, int>)vtbl[22];
        return func(sourceVoice);
    }

    public static void GetSourceVoiceState(nint sourceVoice, out XAUDIO2_VOICE_STATE pVoiceState, uint flags = 0)
    {
        var vtbl = *(void***)sourceVoice;
        var func = (delegate* unmanaged[Stdcall]<nint, out XAUDIO2_VOICE_STATE, uint, void>)vtbl[25];
        func(sourceVoice, out pVoiceState, flags);
    }

    public static int SetFrequencyRatio(nint sourceVoice, float ratio, uint operationSet = XAUDIO2_COMMIT_NOW)
    {
        var vtbl = *(void***)sourceVoice;
        var func = (delegate* unmanaged[Stdcall]<nint, float, uint, int>)vtbl[26];
        return func(sourceVoice, ratio, operationSet);
    }
}
