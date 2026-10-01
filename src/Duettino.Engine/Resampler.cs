namespace Duettino.Engine;

/// <summary>
/// Brings one Source's audio from its own sample rate to 48 kHz, in managed code (ADR-0003), packet after packet:
/// band-limited interpolation with a Kaiser-windowed sinc, its coefficients worked out once per phase.
/// </summary>
/// <remarks>
/// Resampled frame <c>n</c> is the input at time <c>n / 48 kHz</c> after the input's first frame, with no filter delay:
/// it is emitted once the input reaches a few frames past that time, so the last frames pushed are held back until
/// the next push, or until <see cref="Flush"/> when the input stops there.
/// </remarks>
sealed class Resampler
{
    const int ZeroCrossings = 32;
    const double Passband = 0.95;
    const double KaiserBeta = 8;

    readonly int channels;
    readonly int upFactor;   // resampled frames per…
    readonly int downFactor; // …input frames, reduced: resampled frame n sits at input frame n * downFactor / upFactor
    readonly int halfWidth;
    readonly float[][] phases;
    float[] history;
    int historyFrames;
    long historyStart; // input frame index of history[0]
    long inputFrames;
    long outputFrames; // resampled frames emitted so far

    public Resampler(int inputRate, int channels)
    {
        this.channels = channels;
        int gcd = (int)System.Numerics.BigInteger.GreatestCommonDivisor(inputRate, Recording.Rate);
        upFactor = Recording.Rate / gcd;
        downFactor = inputRate / gcd;

        // Cut-off in cycles per input frame, just below the lower of the two Nyquist frequencies.
        double cutoff = 0.5 * Passband * Math.Min(1, (double)Recording.Rate / inputRate);
        halfWidth = (int)Math.Ceiling(ZeroCrossings / (2 * cutoff));
        phases = new float[upFactor][];
        for (int p = 0; p < upFactor; p++) phases[p] = Coefficients((double)p / upFactor, cutoff);

        // The input before the first frame reads as silence.
        history = new float[2 * halfWidth * channels];
        historyFrames = halfWidth;
        historyStart = -halfWidth;
    }

    /// <summary>Where the input pushed so far ends, in 48 kHz frames from the input's first frame.</summary>
    public double InputEnd => (double)inputFrames * upFactor / downFactor;

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
        // Resampled frame n needs the input up to frame floor(n * downFactor / upFactor) + halfWidth.
        long end = inputEnd > 0 ? (inputEnd * upFactor - 1) / downFactor + 1 : 0;
        int count = (int)Math.Max(0, end - outputFrames);
        var result = new float[count * channels];
        for (int i = 0; i < count; i++, outputFrames++)
        {
            long at = outputFrames * downFactor;
            var coefficients = phases[at % upFactor];
            int first = (int)(at / upFactor - halfWidth + 1 - historyStart);
            var frame = result.AsSpan(i * channels, channels);
            for (int k = 0; k < coefficients.Length; k++)
            {
                var source = history.AsSpan((first + k) * channels, channels);
                for (int c = 0; c < channels; c++) frame[c] += coefficients[k] * source[c];
            }
        }
        Discard(outputFrames * downFactor / upFactor - halfWidth + 1);
        return result;
    }

    /// <summary>The taps for a resampled frame <paramref name="fraction"/> of an input frame past its nearest input frame.</summary>
    float[] Coefficients(double fraction, double cutoff)
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
}
