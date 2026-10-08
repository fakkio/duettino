namespace Duettino.Engine;

/// <summary>
/// Works out the resampling correction that keeps a Source on its capture timestamps when its clock drifts from them
/// (virtual devices by a few hundred ppm, real hardware by up to ~100 ppm): a slow PI loop on how far the Source's audio
/// lags behind its timestamps, so the lag is taken back gradually, with no audio cut out or overwritten, and no audible
/// change of pitch (ADR-0005).
/// </summary>
sealed class DriftLoop
{
    /// <summary>Timestamps wobble by a few frames from packet to packet: the lag is smoothed over this long.</summary>
    const double SmoothingSeconds = 0.25;

    /// <summary>A lag this small is left alone, so the jitter of a Source that doesn't drift never bends its audio.</summary>
    const double DeadBand = Recording.Rate / 1000.0;

    // A critically damped loop with a natural frequency of 0.5 rad/s: it settles in a few seconds.
    const double ProportionalGain = 1.0 / Recording.Rate;
    const double IntegralGain = 0.25 / Recording.Rate;

    /// <summary>At most 0.2% faster or slower (3.5 cents): a Source drifting further is re-anchored now and then.</summary>
    const double MaxCorrection = 0.002;

    double smoothedLag;
    double learnedDrift; // the integral term: the drift the Source has shown so far

    /// <summary>
    /// Takes the <paramref name="lag"/>, in 48 kHz frames, of a packet lasting <paramref name="seconds"/>: how far
    /// its timestamp is ahead of where the Source's audio ends. Returns the correction for the Source's resampler.
    /// </summary>
    public double Update(double lag, double seconds)
    {
        smoothedLag += (lag - smoothedLag) * seconds / (SmoothingSeconds + seconds);
        double excess = smoothedLag - Math.Clamp(smoothedLag, -DeadBand, DeadBand);
        learnedDrift = Math.Clamp(learnedDrift + IntegralGain * excess * seconds, -MaxCorrection, MaxCorrection);
        return Math.Clamp(ProportionalGain * excess + learnedDrift, -MaxCorrection, MaxCorrection);
    }

    /// <summary>
    /// The Source's audio starts again at its timestamp, with no lag; its device, and so its drift, are the same.
    /// Returns the correction to start with.
    /// </summary>
    public double Reanchor()
    {
        smoothedLag = 0;
        return learnedDrift;
    }

    /// <summary>The Source switched to a device in another format, whose drift is still unknown.</summary>
    public void ForgetDrift() => smoothedLag = learnedDrift = 0;
}
