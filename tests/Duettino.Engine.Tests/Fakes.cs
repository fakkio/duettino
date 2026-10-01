using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Duettino.Engine.Tests;

/// <summary>A clock the test moves by hand. Ticks are 100 ns, the timebase of packet capture timestamps.</summary>
sealed class FakeClock(DateTime localNow) : IClock
{
    // Arbitrary, non-zero, so tests don't pass by assuming the timebase starts at 0.
    public long Now { get; private set; } = 123_456_789_000;

    public DateTime LocalNow { get; private set; } = localNow;

    public void AdvanceMs(double ms)
    {
        long ticks = (long)Math.Round(ms * TimeSpan.TicksPerSecond / 1000);
        Now += ticks;
        LocalNow = LocalNow.AddTicks(ticks);
    }
}

/// <summary>Synthetic 48 kHz Source packets.</summary>
static class Packets
{
    public static readonly SourceFormat MonoFloat = new(48000, 1, SampleType.Float32);
    public static readonly SourceFormat StereoFloat = new(48000, 2, SampleType.Float32);

    /// <summary>Float32 bytes for <paramref name="frames"/> frames; <paramref name="sample"/>(frame, channel) gives each value.</summary>
    public static byte[] Float(int frames, int channels, Func<int, int, float> sample)
    {
        var values = new float[frames * channels];
        for (int i = 0; i < frames; i++)
            for (int c = 0; c < channels; c++)
                values[i * channels + c] = sample(i, c);
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }
}

/// <summary>Working files written with NAudio, an independent WAV writer, as a Recording would have left them.</summary>
static class WorkingFiles
{
    /// <summary>Writes <paramref name="frames"/> frames; <paramref name="frame"/>(index) gives (Input, Output left, Output right).</summary>
    public static void Write(string path, int frames, Func<int, (short Input, short Left, short Right)> frame)
    {
        var samples = new short[frames * 3];
        for (int i = 0; i < frames; i++)
            (samples[3 * i], samples[3 * i + 1], samples[3 * i + 2]) = frame(i);
        using var writer = new WaveFileWriter(path, new WaveFormat(48000, 16, 3));
        writer.WriteSamples(samples, 0, samples.Length);
    }
}

/// <summary>An MP3 encoder that keeps the Mix it is handed and writes a recognisable stand-in for the MP3.</summary>
sealed class CapturingEncoder : IMp3Encoder
{
    public static readonly byte[] Mp3Bytes = "not really an MP3"u8.ToArray();

    public short[] Mix { get; private set; } = [];

    public void Encode(Stream mix, Stream destination)
    {
        using var copy = new MemoryStream();
        mix.CopyTo(copy);
        Mix = MemoryMarshal.Cast<byte, short>(copy.ToArray().AsSpan()).ToArray();
        destination.Write(Mp3Bytes);
    }
}

/// <summary>An MP3 encoder that breaks halfway, after writing part of the MP3.</summary>
sealed class FailingEncoder : IMp3Encoder
{
    public void Encode(Stream mix, Stream destination)
    {
        var half = new byte[mix.Length / 2];
        mix.ReadExactly(half);
        destination.Write(half);
        throw new IOException("The disk is full.");
    }
}

/// <summary>An MP3 encoder on a machine that has none, as a Windows N edition without the Media Feature Pack.</summary>
sealed class UnavailableEncoder : IMp3Encoder
{
    public void Encode(Stream mix, Stream destination) => throw new Mp3EncoderUnavailableException("No MP3 encoder.");
}

/// <summary>A 16-bit WAV file read back with NAudio, an independent WAV reader.</summary>
sealed record WavFile(WaveFormat Format, short[] Samples)
{
    public static WavFile Read(string path)
    {
        // ReadWrite sharing: the file may still be open for writing by a running Recording.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new WaveFileReader(stream);
        var bytes = new byte[reader.Length];
        reader.ReadExactly(bytes);
        return new WavFile(reader.WaveFormat, MemoryMarshal.Cast<byte, short>(bytes).ToArray());
    }
}

/// <summary>A Working file read back with NAudio, an independent WAV reader.</summary>
sealed record WorkingFile(WaveFormat Format, short[] Samples)
{
    public int Frames => Samples.Length / Format.Channels;

    public short Input(int frame) => Samples[frame * 3];
    public short OutputLeft(int frame) => Samples[frame * 3 + 1];
    public short OutputRight(int frame) => Samples[frame * 3 + 2];

    public static WorkingFile Read(string path)
    {
        var wav = WavFile.Read(path);
        return new WorkingFile(wav.Format, wav.Samples);
    }
}
