namespace Duettino.Engine.Tests;

/// <summary>Leveling, observed in the Mix a Finalization hands to the encoder.</summary>
public sealed class LevelingTests : IDisposable
{
    const int Rate = 48000;

    readonly string folder = Path.Combine(Path.GetTempPath(), "duettino-tests", Guid.NewGuid().ToString("N"));

    public LevelingTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    string WorkingFilePath => Path.Combine(folder, "Duettino_2026-10-03_10-00-00.working.wav");

    /// <summary>A sine of <paramref name="amplitude"/> (1 = full scale) at frame <paramref name="frame"/>.</summary>
    static short Tone(int frame, double hz, double amplitude) =>
        (short)Math.Round(amplitude * 32767 * Math.Sin(2 * Math.PI * hz * frame / Rate));

    static bool Within(int frame, double fromSeconds, double toSeconds) => frame >= fromSeconds * Rate && frame < toSeconds * Rate;

    /// <summary>The RMS level, in dBFS, of the Mix's left channel between two times.</summary>
    static double MixLevel(short[] mix, double fromSeconds, double toSeconds)
    {
        double sum = 0;
        int from = (int)(fromSeconds * Rate), to = (int)(toSeconds * Rate);
        for (int i = from; i < to; i++) sum += (double)mix[2 * i] * mix[2 * i];
        return 10 * Math.Log10(sum / (to - from) / (32768.0 * 32768.0));
    }

    short[] Finalize(int seconds, Func<int, (short Input, short Left, short Right)> frame)
    {
        WorkingFiles.Write(WorkingFilePath, seconds * Rate, frame);
        var encoder = new CapturingEncoder();
        Finalization.Run(WorkingFilePath, encoder);
        return encoder.Mix;
    }

    [Fact]
    public void Two_Sources_at_very_different_loudness_come_out_at_comparable_loudness()
    {
        // A conversation: I speak loudly for 10 s, then the other side, 20 dB quieter, answers for 10 s.
        var mix = Finalize(20, i => Within(i, 0, 10)
            ? (Tone(i, 300, 0.5), 0, 0)
            : (0, Tone(i, 500, 0.05), Tone(i, 500, 0.05)));

        Assert.InRange(MixLevel(mix, 2, 8) - MixLevel(mix, 12, 18), -1, 1);
    }

    [Fact]
    public void Long_stretches_of_exact_digital_zero_do_not_affect_the_gain()
    {
        // A Bluetooth headset microphone sends exact zeros between phrases: here 40 s of them while the other side
        // speaks, against 20 s of my voice. As loud as each other before Leveling, as loud as each other after it.
        var mix = Finalize(60, i => Within(i, 10, 50)
            ? (0, Tone(i, 500, 0.1), Tone(i, 500, 0.1))
            : (Tone(i, 300, 0.1), 0, 0));

        Assert.InRange(MixLevel(mix, 2, 8) - MixLevel(mix, 20, 40), -1, 1);
        Assert.InRange(MixLevel(mix, 52, 58) - MixLevel(mix, 20, 40), -1, 1);
    }

    [Fact]
    public void A_near_silent_Input_is_boosted_by_at_most_12_dB()
    {
        // Room noise from a microphone nobody speaks into, about -63 dBFS: far below any speech.
        const double amplitude = 0.001;
        var mix = Finalize(10, i => (Tone(i, 300, amplitude), 0, 0));

        double inputLevel = 20 * Math.Log10(amplitude * 32767 / 32768 / Math.Sqrt(2));
        Assert.InRange(MixLevel(mix, 2, 8) - inputLevel, 11.5, 12.05);
    }

    [Fact]
    public void A_loudness_change_halfway_through_a_Source_is_followed_by_the_gain()
    {
        // The call app's volume is turned down by 18 dB halfway through.
        var mix = Finalize(60, i => (0, Tone(i, 500, i < 30 * Rate ? 0.4 : 0.05), Tone(i, 500, i < 30 * Rate ? 0.4 : 0.05)));

        Assert.InRange(MixLevel(mix, 2, 12) - MixLevel(mix, 48, 58), -1, 1);
    }

    [Fact]
    public void Room_noise_while_I_stay_silent_is_not_boosted_more_than_my_voice()
    {
        // Listening to a webinar: I speak for 5 s at the start and at the end, and in between my microphone only picks
        // up room noise, 34 dB below my voice.
        const double voice = 0.07, noise = 0.0014;
        var mix = Finalize(60, i => (Tone(i, 300, Within(i, 5, 55) ? noise : voice), 0, 0));

        double voiceGain = MixLevel(mix, 1, 4) - 20 * Math.Log10(voice / Math.Sqrt(2));
        double noiseGain = MixLevel(mix, 20, 40) - 20 * Math.Log10(noise / Math.Sqrt(2));
        Assert.True(noiseGain <= voiceGain + 0.5, $"room noise gets {noiseGain:F1} dB, my voice {voiceGain:F1} dB");
    }
}
