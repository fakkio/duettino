using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Duettino.Engine;

/// <summary>
/// Reads the frames of a Working file: 16-bit PCM WAV, 48 kHz, 3 channels (Input mono, then Output stereo).
/// The audio runs to the end of the file, truncated to whole frames: the header's lengths are ignored, since they lag
/// the data by up to a second when the Recording never reached Stop (ADR-0004).
/// </summary>
sealed class WorkingFileReader : IDisposable
{
    public const int Channels = 3;
    const int FrameBytes = Channels * sizeof(short);

    readonly FileStream stream;
    long framesLeft;

    public WorkingFileReader(string path)
    {
        stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            long dataStart = FindData();
            Frames = (stream.Length - dataStart) / FrameBytes;
            framesLeft = Frames;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public long Frames { get; }

    /// <summary>Reads whole frames into <paramref name="samples"/>; returns how many, 0 at the end.</summary>
    public int Read(Span<short> samples)
    {
        int frames = (int)Math.Min(samples.Length / Channels, framesLeft);
        stream.ReadExactly(MemoryMarshal.AsBytes(samples[..(frames * Channels)]));
        framesLeft -= frames;
        return frames;
    }

    public void Dispose() => stream.Dispose();

    /// <summary>Checks the format and returns the offset of the first audio byte.</summary>
    long FindData()
    {
        Span<byte> header = stackalloc byte[12];
        stream.ReadExactly(header);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..].SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Not a WAV file.");

        bool formatChecked = false;
        Span<byte> chunk = stackalloc byte[8];
        Span<byte> fmt = stackalloc byte[16];
        while (true)
        {
            stream.ReadExactly(chunk);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
            if (chunk[..4].SequenceEqual("data"u8))
            {
                if (!formatChecked) throw new InvalidDataException("The WAV file has no format before its data.");
                return stream.Position;
            }
            if (chunk[..4].SequenceEqual("fmt "u8))
            {
                if (size < fmt.Length) throw new InvalidDataException("The WAV file's format is truncated.");
                stream.ReadExactly(fmt);
                CheckFormat(fmt);
                formatChecked = true;
                size -= 16;
            }
            stream.Seek(size + (size & 1), SeekOrigin.Current);
        }
    }

    static void CheckFormat(ReadOnlySpan<byte> fmt)
    {
        var tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
        if (tag != 1 || channels != Channels || rate != Recording.Rate || bits != 16)
            throw new InvalidDataException(
                $"Not a Working file: format {tag}, {channels} channels, {rate} Hz, {bits}-bit instead of PCM, 3 channels, 48000 Hz, 16-bit.");
    }
}
