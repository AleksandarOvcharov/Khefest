using System.Buffers.Binary;
using System.Text;

namespace Khefest.Audio.Wave;

/// <summary>
/// Parser for Microsoft RIFF WAV files supporting standard PCM (8, 16, 24, 32-bit) and IEEE 32-bit float audio streams.
/// </summary>
public static class WavParser
{
    private const uint RiffFourCc = 0x46464952; // 'RIFF' in little-endian
    private const uint WaveFourCc = 0x45564157; // 'WAVE' in little-endian
    private const uint FmtFourCc  = 0x20746D66; // 'fmt ' in little-endian
    private const uint DataFourCc = 0x61746164; // 'data' in little-endian

    private const ushort WaveFormatPcm = 1;
    private const ushort WaveFormatIeeeFloat = 3;
    private const ushort WaveFormatExtensible = 0xFFFE;

    /// <summary>
    /// Parses a WAV stream from a byte array.
    /// </summary>
    public static WaveData Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        return Parse(stream);
    }

    /// <summary>
    /// Parses a WAV stream from a file on disk.
    /// </summary>
    public static WaveData Parse(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"WAV file not found at '{filePath}'.", filePath);

        using var stream = File.OpenRead(filePath);
        return Parse(stream);
    }

    /// <summary>
    /// Parses a WAV stream from any readable Stream.
    /// </summary>
    public static WaveData Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        if (stream.Length < 12)
            throw new InvalidDataException("Invalid WAV file: stream is too short for RIFF header.");

        // 1. Read RIFF header
        uint riff = reader.ReadUInt32();
        if (riff != RiffFourCc)
            throw new InvalidDataException($"Invalid WAV file: Expected 'RIFF' chunk header, got 0x{riff:X8}.");

        uint fileSizeMinus8 = reader.ReadUInt32();
        _ = fileSizeMinus8;

        uint wave = reader.ReadUInt32();
        if (wave != WaveFourCc)
            throw new InvalidDataException($"Invalid WAV file: Expected 'WAVE' format identifier, got 0x{wave:X8}.");

        // 2. Iterate chunks
        ushort audioFormat = 0;
        ushort channels = 0;
        uint sampleRate = 0;
        uint byteRate = 0;
        ushort blockAlign = 0;
        ushort bitsPerSample = 0;
        byte[]? pcmData = null;

        while (stream.Position + 8 <= stream.Length)
        {
            uint chunkId = reader.ReadUInt32();
            uint chunkSize = reader.ReadUInt32();

            long chunkDataStart = stream.Position;
            long nextChunkPos = chunkDataStart + chunkSize;
            // Chunks in RIFF are word-aligned (padded to 2 bytes)
            if ((chunkSize & 1) != 0)
                nextChunkPos++;

            if (chunkId == FmtFourCc)
            {
                if (chunkSize < 16)
                    throw new InvalidDataException($"Invalid 'fmt ' chunk size: {chunkSize} (expected at least 16 bytes).");

                audioFormat = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadUInt32();
                byteRate = reader.ReadUInt32();
                blockAlign = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();

                // If extensible format, read subformat GUID
                if (audioFormat == WaveFormatExtensible && chunkSize >= 40)
                {
                    ushort cbSize = reader.ReadUInt16();
                    ushort validBitsPerSample = reader.ReadUInt16();
                    uint channelMask = reader.ReadUInt32();
                    ushort subFormatCode = reader.ReadUInt16();
                    audioFormat = subFormatCode;
                }
            }
            else if (chunkId == DataFourCc)
            {
                if (chunkSize > (stream.Length - stream.Position))
                {
                    // Truncated data chunk - read available
                    pcmData = reader.ReadBytes((int)(stream.Length - stream.Position));
                }
                else
                {
                    pcmData = reader.ReadBytes((int)chunkSize);
                }
            }

            // Seek accurately to the next chunk
            if (stream.Position != nextChunkPos)
            {
                if (nextChunkPos <= stream.Length)
                    stream.Position = nextChunkPos;
                else
                    break;
            }
        }

        if (audioFormat == 0 || channels == 0 || sampleRate == 0)
            throw new InvalidDataException("Invalid WAV file: missing or incomplete 'fmt ' chunk.");

        if (pcmData == null)
            throw new InvalidDataException("Invalid WAV file: missing 'data' chunk.");

        AudioSampleFormat sampleFormat;
        if (audioFormat == WaveFormatPcm)
        {
            sampleFormat = bitsPerSample switch
            {
                8 => AudioSampleFormat.Pcm8,
                16 => AudioSampleFormat.Pcm16,
                24 => AudioSampleFormat.Pcm24,
                32 => AudioSampleFormat.Pcm32,
                _ => throw new NotSupportedException($"Unsupported PCM bit depth: {bitsPerSample}-bit.")
            };
        }
        else if (audioFormat == WaveFormatIeeeFloat && bitsPerSample == 32)
        {
            sampleFormat = AudioSampleFormat.IeeeFloat32;
        }
        else
        {
            throw new NotSupportedException($"Unsupported WAV audio format code: 0x{audioFormat:X4}, {bitsPerSample}-bit.");
        }

        return new WaveData((int)sampleRate, channels, sampleFormat, bitsPerSample, pcmData);
    }
}
