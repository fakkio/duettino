using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Duettino.Engine;

/// <summary>
/// Writes the Working file: a 16-bit PCM WAV whose header is rewritten on every <see cref="Flush"/>,
/// so the file stays readable if the process or the PC dies (ADR-0004).
/// </summary>
sealed class WorkingFileWriter : IDisposable
{
    const int HeaderBytes = 44;

    readonly FileStream stream;
    readonly int channels;
    readonly int sampleRate;
    long dataBytes;

    public WorkingFileWriter(string path, int sampleRate, int channels)
    {
        this.sampleRate = sampleRate;
        this.channels = channels;
        stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        WriteHeader();
    }

    public void Write(ReadOnlySpan<short> samples)
    {
        stream.Write(MemoryMarshal.AsBytes(samples));
        dataBytes += samples.Length * sizeof(short);
    }

    /// <summary>Brings the header up to date with the data written so far and pushes both to disk.</summary>
    public void Flush()
    {
        stream.Seek(0, SeekOrigin.Begin);
        WriteHeader();
        stream.Seek(0, SeekOrigin.End);
        stream.Flush(flushToDisk: true);
    }

    public void Dispose()
    {
        Flush();
        stream.Dispose();
    }

    void WriteHeader()
    {
        int blockAlign = channels * sizeof(short);
        Span<byte> h = stackalloc byte[HeaderBytes];
        "RIFF"u8.CopyTo(h);
        BinaryPrimitives.WriteUInt32LittleEndian(h[4..], (uint)(HeaderBytes - 8 + dataBytes));
        "WAVE"u8.CopyTo(h[8..]);
        "fmt "u8.CopyTo(h[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(h[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(h[20..], 1); // WAVE_FORMAT_PCM
        BinaryPrimitives.WriteUInt16LittleEndian(h[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(h[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(h[28..], (uint)(sampleRate * blockAlign));
        BinaryPrimitives.WriteUInt16LittleEndian(h[32..], (ushort)blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(h[34..], 16);
        "data"u8.CopyTo(h[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(h[40..], (uint)dataBytes);
        stream.Write(h);
    }
}
