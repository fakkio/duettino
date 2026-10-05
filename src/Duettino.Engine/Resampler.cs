using System.Collections.Concurrent;

namespace Duettino.Engine;

/// <summary>
/// Brings one Source's audio from its own sample rate to 48 kHz, in managed code (ADR-0003), packet after packet:
/// band-limited interpolation with a Kaiser-windowed sinc, its coefficients worked out once on a fine grid of phases.
/// The ratio can be nudged by a <see cref="Correction"/> while it runs, to follow a Source whose clock drifts.
/// </summary>
/// <remarks>
/// Resampled frame <c>n</c> is the input at time <c>n / 48 kHz</c> after the input's first frame (stretched by the
/// correction in force), with no filter delay: it is emitted once the input reaches a few frames past that time, so the
/// last frames pushed are held back until the next push, or until <see cref="Flush"/> when the input stops there.
/// At 48 kHz with no correction, every frame comes out exactly as it went in.
/// </remarks>
sealed class Resampler
{
    const int ZeroCrossings = 32;
    const double Passband = 0.95;
    const double KaiserBeta = 8;
    const int PhaseCount = 512;

    static readonly ConcurrentDictionary<int, Kernel> Kernels = new();

    readonly int channels;
    readonly double nominalStep; // input frames per resampled frame
    readonly int halfWidth;
    readonly float[][] phases; // the taps at fraction p / PhaseCount of an input frame past the nearest input frame, p = 0..PhaseCount
    readonly float[] taps;
    double step;
    float[] history;
    int historyFrames;
    long historyStart; // input frame index of history[0]
    long inputFrames;
    long outputFrames; // resampled frames emitted so far
    long nextWhole;    // the next resampled frame sits at input frame nextWhole + nextFraction
    double nextFraction;

    public Resampler(int inputRate, int channels)
    {
        this.channels = channels;
        nominalStep = step = (double)inputRate / Recording.Rate;
        (halfWidth, phases) = Kernels.GetOrAdd(inputRate, Kernel.For);
        taps = new float[2 * halfWidth];

        // The input before the first frame reads as silence.
        history = new float[2 * halfWidth * channels];
        historyFrames = halfWidth;
        historyStart = -halfWidth;
    }

    /// <summary>
    /// How much the resampled audio is stretched from now on: 0.001 makes 0.1% more resampled frames out of the same
    /// input, -0.001 0.1% fewer.
    /// </summary>
    public double Correction
    {
        set => step = nominalStep / (1 + value);
    }

    /// <summary>Where the input pushed so far ends, in 48 kHz frames from the input's first frame.</summary>
    public double InputEnd => outputFrames + (inputFrames - nextWhole - nextFraction) / step;

    /// <summary>Where the resampled frames emitted so far end, in 48 kHz frames from the input's first frame.</summary>
    public long Emitted => outputFrames;

    /// <summary>
    /// Takes the next interleaved input frames and returns the resampled frames now complete, which start
    /// <paramref name="position"/> 48 kHz frames after the input's first frame.
    /// </summary>
    public float[] Push(ReadOnlySpan<float> input, out long position)
    {
        Append(input);
        return Emit(inputFrames - halfWidth, out position);
    }

    /// <summary>
    /// Returns the resampled frames held back for want of later input, as if the input ended here; see
    /// <see cref="Push"/> for <paramref name="position"/>. Nothing can be pushed afterwards.
    /// </summary>
    public float[] Flush(out long position)
    {
        Append(new float[halfWidth * channels]);
        return Emit(inputFrames - halfWidth, out position);
    }

    /// <summary>Computes the resampled frames whose nearest input frame comes before <paramref name="inputEnd"/>.</summary>
    float[] Emit(long inputEnd, out long position)
    {
        position = outputFrames;
        // Resampled frame n needs the input up to its nearest input frame + halfWidth.
        int bound = (int)Math.Max(0, Math.Ceiling((inputEnd - nextWhole - nextFraction) / step) + 1);
        var result = new float[bound * channels];
        int count = 0;
        for (; nextWhole < inputEnd; count++)
        {
            var coefficients = TapsAt(nextFraction);
            int first = (int)(nextWhole - halfWidth + 1 - historyStart) * channels;
            for (int c = 0; c < channels; c++)
            {
                float sum = 0;
                for (int k = 0, at = first + c; k < coefficients.Length; k++, at += channels) sum += coefficients[k] * history[at];
                result[count * channels + c] = sum;
            }
            outputFrames++;
            nextFraction += step;
            double carry = Math.Floor(nextFraction);
            nextWhole += (long)carry;
            nextFraction -= carry;
        }
        Discard(nextWhole - halfWidth + 1);
        return count == bound ? result : result[..(count * channels)];
    }

    /// <summary>The taps for a resampled frame <paramref name="fraction"/> of an input frame past its nearest input frame.</summary>
    float[] TapsAt(double fraction)
    {
        double at = fraction * PhaseCount;
        int phase = (int)at;
        float weight = (float)(at - phase);
        if (weight == 0) return phases[phase];
        var below = phases[phase];
        var above = phases[phase + 1];
        for (int k = 0; k < taps.Length; k++) taps[k] = below[k] + weight * (above[k] - below[k]);
        return taps;
    }

    void Append(ReadOnlySpan<float> input)
    {
        int frames = input.Length / channels;
        if ((historyFrames + frames) * channels > history.Length)
            Array.Resize(ref history, (historyFrames + frames) * channels * 2);
        input[..(frames * channels)].CopyTo(history.AsSpan(historyFrames * channels));
        historyFrames += frames;
        inputFrames += frames;
    }

    /// <summary>Forgets the input before frame <paramref name="from"/>, which no resampled frame still to come needs.</summary>
    void Discard(long from)
    {
        int frames = (int)Math.Clamp(from - historyStart, 0, historyFrames);
        if (frames == 0) return;
        history.AsSpan(frames * channels, (historyFrames - frames) * channels).CopyTo(history);
        historyFrames -= frames;
        historyStart += frames;
    }

    /// <summary>The filter for one input rate, shared by every resampler at that rate.</summary>
    sealed record Kernel(int HalfWidth, float[][] Phases)
    {
        public static Kernel For(int inputRate)
        {
            // Cut-off in cycles per input frame, just below the lower of the two Nyquist frequencies; right on it at
            // 48 kHz, where there is nothing to filter out and the taps must leave the audio as it is.
            double cutoff = inputRate == Recording.Rate ? 0.5 : 0.5 * Passband * Math.Min(1, (double)Recording.Rate / inputRate);
            int halfWidth = (int)Math.Ceiling(ZeroCrossings / (2 * cutoff));
            var phases = new float[PhaseCount + 1][];
            for (int p = 0; p <= PhaseCount; p++) phases[p] = Coefficients((double)p / PhaseCount, cutoff, halfWidth);
            return new Kernel(halfWidth, phases);
        }

        static float[] Coefficients(double fraction, double cutoff, int halfWidth)
        {
            var taps = new float[2 * halfWidth];
            double sum = 0;
            var values = new double[taps.Length];
            for (int k = 0; k < taps.Length; k++)
            {
                double t = fraction + halfWidth - 1 - k; // distance from the tap's input frame, in input frames
                double x = 2 * cutoff * t;
                double sinc = x == 0 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);
                double r = t / halfWidth;
                double window = BesselI0(KaiserBeta * Math.Sqrt(Math.Max(0, 1 - r * r))) / BesselI0(KaiserBeta);
                values[k] = sinc * window;
                sum += values[k];
            }
            // Unity gain at DC for every phase, so a constant level comes out unchanged.
            for (int k = 0; k < taps.Length; k++) taps[k] = (float)(values[k] / sum);
            return taps;
        }

        static double BesselI0(double x)
        {
            double sum = 1, term = 1;
            for (int k = 1; term > 1e-12 * sum; k++)
            {
                term *= x * x / (4.0 * k * k);
                sum += term;
            }
            return sum;
        }
    }
}
