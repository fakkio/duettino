namespace Duettino.Engine;

/// <summary>
/// One Source's audio, held by position on the Recording's timeline until the Working file is written there.
/// It holds at most two seconds ahead of the write position: anything further is dropped, so memory stays bounded.
/// A position nothing was placed at reads as silence, which is how Gaps become silence (ADR-0005).
/// </summary>
sealed class SourceTimeline(int channels)
{
    const int Capacity = 2 * Recording.Rate;

    /// <summary>
    /// A packet starting less than this many frames from where the previous one ended is appended right after it,
    /// so timestamp jitter doesn't chop the audio; beyond it, the packet goes exactly where its timestamp says.
    /// </summary>
    internal const int Tolerance = Recording.Rate / 100;

    // Frame at position p lives at (p % Capacity) * channels; slots are cleared once read.
    readonly float[] ring = new float[Capacity * channels];
    readonly object gate = new();
    long readPosition;
    long? end;

    public int Channels => channels;

    /// <summary>Places interleaved 48 kHz <paramref name="samples"/> starting at timeline <paramref name="position"/>.</summary>
    public void Place(ReadOnlySpan<float> samples, long position)
    {
        int frames = samples.Length / channels;
        lock (gate)
        {
            if (end is { } e && Math.Abs(position - e) < Tolerance) position = e;
            end = position + frames;
            long from = Math.Max(position, readPosition);
            long to = Math.Min(position + frames, readPosition + Capacity);
            for (long p = from; p < to; p++)
                samples.Slice((int)(p - position) * channels, channels).CopyTo(ring.AsSpan((int)(p % Capacity) * channels, channels));
        }
    }

    /// <summary>Reads the next <c>destination.Length / Channels</c> frames of the timeline into <paramref name="destination"/>.</summary>
    public void Read(Span<float> destination)
    {
        int frames = destination.Length / channels;
        lock (gate)
        {
            for (int i = 0; i < frames; i++)
            {
                var slot = ring.AsSpan((int)((readPosition + i) % Capacity) * channels, channels);
                slot.CopyTo(destination.Slice(i * channels, channels));
                slot.Clear();
            }
            readPosition += frames;
        }
    }
}
