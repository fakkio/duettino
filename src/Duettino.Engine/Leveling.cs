namespace Duettino.Engine;

/// <summary>
/// Leveling of a Working file: for each Source, the gain over time that brings it to the same target loudness as the
/// other before the Mix (ADR-0004).
/// </summary>
/// <remarks>
/// Loudness is the mean square of 100 ms blocks, measured over a window of <see cref="WindowBlocks"/> blocks centred
/// on each block, so the gain follows a volume that changes mid-Recording. Only active blocks count: digital zero and
/// what lies far below the Source's own loudness are left out. Each frame's gain is interpolated between the gains of
/// the blocks around it, so it never jumps.
/// </remarks>
sealed class Leveling
{
    /// <summary>The loudness both Sources are brought to: -20 dBFS RMS, as a mean square.</summary>
    const double TargetMeanSquare = 0.01;

    /// <summary>Loudness is measured in blocks of 100 ms.</summary>
    const int BlockFrames = Recording.Rate / 10;

    /// <summary>The window loudness is measured over: 10 s, longer than a sentence, shorter than a volume change.</summary>
    const int WindowBlocks = 100;

    /// <summary>
    /// The highest gain, +12 dB: a Source far below the target (room noise from a microphone nobody speaks into) is
    /// raised only this much, so its noise isn't blown up. Attenuation has no limit.
    /// </summary>
    const float MaxGain = 3.981f;

    /// <summary>
    /// How far below a Source's loudness a block still counts as active: 30 dB, as a mean-square ratio. Speech and
    /// music sit well within it; the room noise of a microphone nobody speaks into sits below it.
    /// </summary>
    const double ActivityRange = 0.001;

    /// <summary>The relative gate of a Source's overall loudness: 10 dB, as a mean-square ratio.</summary>
    const double RelativeGate = 0.1;

    Leveling(GainCurve input, GainCurve output)
    {
        Input = input;
        Output = output;
    }

    /// <summary>The Input's gain over time.</summary>
    public GainCurve Input { get; }

    /// <summary>The Output's gain over time.</summary>
    public GainCurve Output { get; }

    /// <summary>Reads the whole Working file at <paramref name="workingFilePath"/> and works out each Source's gain.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public static Leveling Measure(string workingFilePath, CancellationToken cancellation = default)
    {
        var inputBlocks = new List<Block>();
        var outputBlocks = new List<Block>();
        using var reader = new WorkingFileReader(workingFilePath);
        var samples = new short[BlockFrames * WorkingFileReader.Channels];
        int frames;
        while ((frames = reader.Read(samples)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            double inputEnergy = 0, outputEnergy = 0;
            for (int i = 0; i < frames; i++)
            {
                var frame = samples.AsSpan(i * WorkingFileReader.Channels, WorkingFileReader.Channels);
                double input = frame[0] / 32768.0, left = frame[1] / 32768.0, right = frame[2] / 32768.0;
                inputEnergy += input * input;
                outputEnergy += (left * left + right * right) / 2;
            }
            inputBlocks.Add(new(inputEnergy, frames));
            outputBlocks.Add(new(outputEnergy, frames));
        }
        return new(Curve(inputBlocks), Curve(outputBlocks));
    }

    /// <summary>
    /// Each block's gain, from the loudness of the active blocks in the window around it. Where a window holds none
    /// (a long silence, room noise only), the gain moves straight from the last active stretch to the next one.
    /// </summary>
    static GainCurve Curve(List<Block> blocks)
    {
        var gains = new float[blocks.Count];
        var defined = new bool[blocks.Count];
        double activity = ActivityThreshold(blocks);
        for (int b = 0; b < blocks.Count; b++)
        {
            double energy = 0;
            long frames = 0;
            int first = Math.Max(0, b - WindowBlocks / 2), last = Math.Min(blocks.Count - 1, b + WindowBlocks / 2);
            for (int w = first; w <= last; w++)
            {
                if (blocks[w].IsDigitalZero || blocks[w].MeanSquare < activity) continue;
                energy += blocks[w].Energy;
                frames += blocks[w].Frames;
            }
            if (frames == 0) continue;
            gains[b] = Math.Min(MaxGain, (float)Math.Sqrt(TargetMeanSquare / (energy / frames)));
            defined[b] = true;
        }
        FillGaps(gains, defined);
        return new(gains);
    }

    /// <summary>
    /// The mean square under which a block isn't active: <see cref="ActivityRange"/> below the Source's loudness over
    /// the whole Recording. That loudness is gated itself, leaving out the blocks more than <see cref="RelativeGate"/>
    /// below the plain mean (as EBU R128 does), so that hours of room noise don't drag it down to their own level.
    /// </summary>
    static double ActivityThreshold(List<Block> blocks)
    {
        var sounding = blocks.Where(b => !b.IsDigitalZero).ToList();
        if (sounding.Count == 0) return 0;
        double mean = MeanSquare(sounding);
        return MeanSquare(sounding.Where(b => b.MeanSquare >= mean * RelativeGate)) * ActivityRange;
    }

    static double MeanSquare(IEnumerable<Block> blocks) =>
        blocks.Sum(b => b.Energy) / blocks.Sum(b => (long)b.Frames);

    /// <summary>
    /// Gives each block without a gain one interpolated between the nearest blocks with one, or the nearest one's
    /// gain before the first and after the last; with none at all (a Source that is all digital zero), unity.
    /// </summary>
    static void FillGaps(float[] gains, bool[] defined)
    {
        int previous = -1;
        for (int b = 0; b <= gains.Length; b++)
        {
            if (b < gains.Length && !defined[b]) continue;
            for (int gap = previous + 1; gap < b; gap++)
            {
                gains[gap] = (previous, b == gains.Length) switch
                {
                    (-1, true) => 1,
                    (-1, false) => gains[b],
                    (_, true) => gains[previous],
                    _ => gains[previous] + (gains[b] - gains[previous]) * (gap - previous) / (b - previous),
                };
            }
            previous = b;
        }
    }

    /// <summary>The sum of the squared samples of a block, and how many frames it holds (the last one may be short).</summary>
    readonly record struct Block(double Energy, int Frames)
    {
        /// <summary>
        /// A block of exact digital zero is no audio at all (a Gap, a paused player, a Bluetooth microphone between
        /// phrases), not a quiet Source: it would only drag the measure down.
        /// </summary>
        public bool IsDigitalZero => Energy == 0;

        public double MeanSquare => Energy / Frames;
    }

    /// <summary>One Source's gain, block by block.</summary>
    public sealed class GainCurve
    {
        readonly float[] gains;

        public GainCurve(float[] gains) => this.gains = gains;

        /// <summary>The gain at <paramref name="frame"/>, interpolated between the centres of the blocks around it.</summary>
        public float At(long frame)
        {
            if (gains.Length == 0) return 1;
            double position = (frame - BlockFrames / 2.0) / BlockFrames;
            if (position <= 0) return gains[0];
            if (position >= gains.Length - 1) return gains[^1];
            int before = (int)position;
            float fraction = (float)(position - before);
            return gains[before] + (gains[before + 1] - gains[before]) * fraction;
        }
    }
}
