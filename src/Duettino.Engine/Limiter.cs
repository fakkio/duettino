namespace Duettino.Engine;

/// <summary>
/// A stereo-linked look-ahead peak limiter: keeps every sample under <see cref="Ceiling"/> without flattening the
/// waveform, by lowering the gain smoothly over the <see cref="Lookahead"/> frames before a peak and raising it back
/// with an exponential release. Its output is delayed by <see cref="Delay"/> frames.
/// </summary>
/// <remarks>
/// The gain needed by each frame is taken as the minimum over the window of frames it is about to meet, then averaged
/// over a window of the same length: every frame of that average is still at most the gain the peak needs, and the
/// gain ramps linearly instead of jumping.
/// </remarks>
sealed class Limiter
{
    const int Lookahead = Recording.Rate / 200; // 5 ms
    const float Ceiling = 0.891f; // -1 dBFS
    const double ReleaseSeconds = 0.1;

    /// <summary>How much of the gain reduction is left after one frame of release.</summary>
    static readonly float ReleasePerFrame = (float)Math.Exp(-1 / (ReleaseSeconds * Recording.Rate));

    /// <summary>How many frames late a frame comes out of <see cref="Process"/>.</summary>
    public const int Delay = Lookahead - 1;

    readonly float[] samples = new float[Lookahead * 2];
    readonly long[] minFrames = new long[Lookahead];
    readonly float[] minGains = new float[Lookahead];
    readonly float[] gains = new float[Lookahead];
    int minHead;
    int minCount;
    double gainSum = Lookahead;
    float released = 1;
    long frame;

    public Limiter() => Array.Fill(gains, 1f);

    /// <summary>Takes one frame in and gives back the frame <see cref="Delay"/> frames older, limited.</summary>
    public (float Left, float Right) Process(float left, float right)
    {
        int slot = (int)(frame % Lookahead);
        samples[2 * slot] = left;
        samples[2 * slot + 1] = right;

        float peak = Math.Max(Math.Abs(left), Math.Abs(right));
        float needed = peak > Ceiling ? Ceiling / peak : 1;
        float windowMin = SlidingMin(needed);

        released = Math.Min(windowMin, 1 - (1 - released) * ReleasePerFrame);
        gainSum += released - gains[slot];
        gains[slot] = released;
        float gain = (float)(gainSum / Lookahead);

        int oldest = (int)((frame + 1) % Lookahead);
        frame++;
        return (samples[2 * oldest] * gain, samples[2 * oldest + 1] * gain);
    }

    /// <summary>Adds the gain needed by the current frame; returns the minimum over the last <see cref="Lookahead"/> frames.</summary>
    float SlidingMin(float needed)
    {
        // A monotonic queue: frames in order, gains increasing; anything a newer, lower gain hides is dropped.
        if (minCount > 0 && frame - minFrames[minHead] >= Lookahead)
        {
            minHead = (minHead + 1) % Lookahead;
            minCount--;
        }
        while (minCount > 0 && minGains[(minHead + minCount - 1) % Lookahead] >= needed) minCount--;
        int tail = (minHead + minCount) % Lookahead;
        minFrames[tail] = frame;
        minGains[tail] = needed;
        minCount++;
        return minGains[minHead];
    }
}
