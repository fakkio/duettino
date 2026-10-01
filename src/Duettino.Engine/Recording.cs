using System.Globalization;

namespace Duettino.Engine;

/// <summary>
/// One Recording, from Record to Stop. Paced by its own clock, not by the data the Sources deliver: each packet is
/// placed at its capture timestamp, and the Working file is written a fixed safety latency behind the clock, with
/// silence wherever a Source delivered nothing (ADR-0005).
/// </summary>
/// <remarks>
/// <see cref="Deliver"/> may be called from any thread (typically one per Source);
/// <see cref="Advance"/> is called regularly by whoever paces the Recording.
/// </remarks>
public sealed class Recording : IDisposable
{
    internal const int Rate = 48000;
    internal const string WorkingFileSuffix = ".working.wav";
    const long SafetyLatencyTicks = TimeSpan.TicksPerSecond / 4;
    const int ChunkFrames = Rate / 2;

    readonly IClock clock;
    readonly long startTicks;
    readonly WavFileWriter writer;
    readonly SourceTimeline input = new(channels: 1);
    readonly SourceTimeline output = new(channels: 2);
    readonly float[] inputChunk = new float[ChunkFrames];
    readonly float[] outputChunk = new float[ChunkFrames * 2];
    readonly short[] fileChunk = new short[ChunkFrames * 3];
    readonly object gate = new();
    long writtenFrames;
    long flushedFrames;
    long? endFrame;
    bool closed;

    Recording(string folder, IClock clock)
    {
        this.clock = clock;
        Directory.CreateDirectory(folder);
        var stem = "Duettino_" + clock.LocalNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        WorkingFilePath = Path.Combine(folder, stem + WorkingFileSuffix);
        writer = new WavFileWriter(WorkingFilePath, Rate, channels: 3);
        startTicks = clock.Now;
    }

    /// <summary>
    /// Starts a Recording now: opens its Working file in <paramref name="folder"/>, named after the clock's local time.
    /// </summary>
    public static Recording Start(string folder, IClock clock) => new(folder, clock);

    /// <summary>The Working file: WAV, 48 kHz, 16-bit, 3 channels (Input mono, then Output stereo).</summary>
    public string WorkingFilePath { get; }

    /// <summary>True once the Recording was stopped and the Working file written up to the Stop time and closed.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Hands over one packet captured from a Source: interleaved samples in <paramref name="format"/>, whose first
    /// frame was captured at <paramref name="captureTime"/> (100 ns ticks, the timebase of <see cref="IClock.Now"/>).
    /// </summary>
    public void Deliver(Source source, SourceFormat format, ReadOnlySpan<byte> data, long captureTime, PacketFlags flags = PacketFlags.None)
    {
        var timeline = source == Source.Input ? input : output;
        var samples = FormatConversion.Convert(format, data, flags, timeline.Channels);
        // An empty packet carries no audio, and its timestamp can be garbage (NAudio hands one over, stamped 0, after
        // every real packet): placing it would lose track of where the Source's audio ends.
        if (samples.Length == 0) return;
        timeline.Place(samples, FrameAt(captureTime));
    }

    /// <summary>Writes the Working file up to the safety latency behind the clock. Call it every few milliseconds.</summary>
    public void Advance()
    {
        lock (gate)
        {
            if (closed) return;
            long target = FrameAt(clock.Now - SafetyLatencyTicks);
            if (endFrame is { } end) target = Math.Min(target, end);
            while (writtenFrames < target) WriteChunk((int)Math.Min(target - writtenFrames, ChunkFrames));
            if (writtenFrames - flushedFrames >= Rate)
            {
                writer.Flush();
                flushedFrames = writtenFrames;
            }
            if (writtenFrames == endFrame)
            {
                Close();
                IsCompleted = true;
            }
        }
    }

    /// <summary>
    /// Ends the Recording at the current clock time. Packets still in flight are awaited for the safety latency:
    /// keep calling <see cref="Advance"/> until <see cref="IsCompleted"/>.
    /// </summary>
    public void Stop()
    {
        lock (gate) endFrame ??= FrameAt(clock.Now);
    }

    /// <summary>Closes the Working file at once, keeping what has been written so far.</summary>
    public void Dispose()
    {
        lock (gate) Close();
    }

    long FrameAt(long ticks) => (long)Math.Round((ticks - startTicks) * (double)Rate / TimeSpan.TicksPerSecond);

    void WriteChunk(int frames)
    {
        input.Read(inputChunk.AsSpan(0, frames));
        output.Read(outputChunk.AsSpan(0, frames * 2));
        for (int i = 0; i < frames; i++)
        {
            fileChunk[3 * i] = ToPcm16(inputChunk[i]);
            fileChunk[3 * i + 1] = ToPcm16(outputChunk[2 * i]);
            fileChunk[3 * i + 2] = ToPcm16(outputChunk[2 * i + 1]);
        }
        writer.Write(fileChunk.AsSpan(0, frames * 3));
        writtenFrames += frames;
    }

    static short ToPcm16(float sample) => (short)Math.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue);

    void Close()
    {
        if (closed) return;
        closed = true;
        writer.Dispose();
    }
}
