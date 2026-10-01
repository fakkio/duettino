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

/// <summary>A Working file read back with NAudio, an independent WAV reader.</summary>
sealed record WorkingFile(WaveFormat Format, short[] Samples)
{
    public int Frames => Samples.Length / Format.Channels;

    public short Input(int frame) => Samples[frame * 3];
    public short OutputLeft(int frame) => Samples[frame * 3 + 1];
    public short OutputRight(int frame) => Samples[frame * 3 + 2];

    public static WorkingFile Read(string path)
    {
        // ReadWrite sharing: the file may still be open for writing by a running Recording.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new WaveFileReader(stream);
        var bytes = new byte[reader.Length];
        reader.ReadExactly(bytes);
        return new WorkingFile(reader.WaveFormat, MemoryMarshal.Cast<byte, short>(bytes).ToArray());
    }
}
