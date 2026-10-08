using NAudio.Wave;

namespace Duettino.Engine.Tests;

public sealed class FinalizationTests : IDisposable
{
    const string Stem = "Duettino_2026-10-01_14-30-05";

    readonly string folder = Path.Combine(Path.GetTempPath(), "duettino-tests", Guid.NewGuid().ToString("N"));

    public FinalizationTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    string WorkingFilePath => Path.Combine(folder, Stem + ".working.wav");

    [Fact]
    public void The_Mix_carries_the_Input_on_both_sides_added_to_the_Output_and_lands_in_an_MP3_named_after_the_Working_file()
    {
        // Input and Output equally loud, so Leveling gives them the same gain: the Input cancels the Output's right side.
        WorkingFiles.Write(WorkingFilePath, 4800, _ => (1000, 1000, -1000));
        var encoder = new CapturingEncoder();

        var result = Finalization.Run(WorkingFilePath, encoder, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, Stem + ".mp3"), result.RecordingFilePath);
        Assert.False(result.IsWav);
        Assert.Equal(CapturingEncoder.Mp3Bytes, File.ReadAllBytes(result.RecordingFilePath));
        Assert.False(File.Exists(WorkingFilePath));
        Assert.Equal(4800 * 2, encoder.Mix.Length);
        Assert.InRange(encoder.Mix[0], 1000, 32767);
        Assert.All(Enumerable.Range(0, 4800), i => Assert.Equal((encoder.Mix[0], 0), (encoder.Mix[2 * i], encoder.Mix[2 * i + 1])));
    }

    [Fact]
    public void The_Media_Foundation_encoder_produces_a_48_kHz_stereo_128_kbps_MP3_as_long_as_the_Recording()
    {
        WorkingFiles.Write(WorkingFilePath, 3 * 48000, i => (0, (short)(8000 * Math.Sin(2 * Math.PI * 440 * i / 48000)), 0));

        var result = Finalization.Run(WorkingFilePath, new MediaFoundationMp3Encoder(), TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, Stem + ".mp3"), result.RecordingFilePath);
        Assert.False(File.Exists(WorkingFilePath));
        using var mp3 = File.OpenRead(result.RecordingFilePath);
        var frames = new List<Mp3Frame>();
        while (Mp3Frame.LoadFromStream(mp3) is { } frame) frames.Add(frame);
        Assert.NotEmpty(frames);
        Assert.All(frames, f => Assert.Equal((48000, 128000), (f.SampleRate, f.BitRate)));
        Assert.All(frames, f => Assert.NotEqual(ChannelMode.Mono, f.ChannelMode));
        Assert.InRange(frames.Sum(f => f.SampleCount) / 48000.0, 3.0, 3.1); // the encoder adds a few ms of padding
    }

    [Fact]
    public void Both_Sources_peaking_at_full_scale_together_mix_without_clipping_or_wrapping_around()
    {
        // Short 1 kHz bursts at full scale, 5 ms every 100 ms, on both Sources in phase: peaks far above their
        // loudness, as in speech, which Leveling leaves near full scale. Their plain sum would be twice full scale.
        static short Sine(int frame) =>
            frame % 4800 < 240 ? (short)Math.Round(32767 * Math.Sin(2 * Math.PI * 1000 * frame / 48000)) : (short)0;
        WorkingFiles.Write(WorkingFilePath, 2 * 48000, i => (Sine(i), Sine(i), Sine(i)));
        var encoder = new CapturingEncoder();

        Finalization.Run(WorkingFilePath, encoder, TestContext.Current.CancellationToken);

        var mix = encoder.Mix;
        int peak = mix.Max(s => Math.Abs((int)s));
        Assert.InRange(peak, 16384, 31128); // loud, yet at least 0.5 dB below full scale
        Assert.True(mix.Count(s => Math.Abs((int)s) == peak) < mix.Count(s => s != 0) / 20, "the waveform is flattened at its peak");
        Assert.All(Enumerable.Range(0, mix.Length / 2), i =>
        {
            int sign = Math.Sign(Sine(i));
            if (Math.Abs((int)Sine(i)) > 1000) Assert.Equal((sign, sign), (Math.Sign(mix[2 * i]), Math.Sign(mix[2 * i + 1])));
        });
    }

    [Fact]
    public void A_taken_name_gets_the_first_free_numeric_suffix_and_nothing_is_overwritten()
    {
        WorkingFiles.Write(WorkingFilePath, 480, _ => (0, 0, 0));
        File.WriteAllText(Path.Combine(folder, Stem + ".mp3"), "first");
        File.WriteAllText(Path.Combine(folder, Stem + "_2.mp3"), "second");

        var result = Finalization.Run(WorkingFilePath, new CapturingEncoder(), TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, Stem + "_3.mp3"), result.RecordingFilePath);
        Assert.Equal("first", File.ReadAllText(Path.Combine(folder, Stem + ".mp3")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(folder, Stem + "_2.mp3")));
    }

    [Fact]
    public void Without_an_MP3_encoder_the_Recording_file_is_the_Mix_as_a_stereo_WAV()
    {
        WorkingFiles.Write(WorkingFilePath, 4800, _ => (1000, 1000, -1000));

        var result = Finalization.Run(WorkingFilePath, new UnavailableEncoder(), TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, Stem + ".wav"), result.RecordingFilePath);
        Assert.True(result.IsWav);
        Assert.Equal([result.RecordingFilePath], Directory.GetFiles(folder));
        var wav = WavFile.Read(result.RecordingFilePath);
        Assert.Equal((48000, 16, 2), (wav.Format.SampleRate, wav.Format.BitsPerSample, wav.Format.Channels));
        Assert.Equal(4800 * 2, wav.Samples.Length);
        Assert.InRange(wav.Samples[0], 1000, 32767);
        Assert.All(Enumerable.Range(0, 4800), i => Assert.Equal((wav.Samples[0], 0), (wav.Samples[2 * i], wav.Samples[2 * i + 1])));
    }

    [Fact]
    public void A_failing_encoder_keeps_the_Working_file_and_leaves_no_partial_Recording_file()
    {
        WorkingFiles.Write(WorkingFilePath, 4800, i => ((short)i, 0, 0));
        var before = File.ReadAllBytes(WorkingFilePath);

        Assert.Throws<IOException>(() => Finalization.Run(WorkingFilePath, new FailingEncoder(), TestContext.Current.CancellationToken));

        Assert.Equal(before, File.ReadAllBytes(WorkingFilePath));
        Assert.Equal([WorkingFilePath], Directory.GetFiles(folder));
    }

    [Fact]
    public void While_encoding_no_file_bears_a_Recording_file_name_so_a_Finalization_killed_midway_leaves_none_that_looks_complete()
    {
        WorkingFiles.Write(WorkingFilePath, 4800, i => ((short)i, 0, 0));
        string[] midway = [];
        var encoder = new MidwayEncoder(() => midway = Directory.GetFiles(folder));

        var result = Finalization.Run(WorkingFilePath, encoder, TestContext.Current.CancellationToken);

        Assert.Equal(2, midway.Length); // the Working file and the one being encoded into
        Assert.Contains(WorkingFilePath, midway);
        Assert.All(midway.Where(f => f != WorkingFilePath), f => Assert.DoesNotContain(Path.GetExtension(f), new[] { ".mp3", ".wav" }));
        Assert.Equal([Path.Combine(folder, Stem + ".mp3")], Directory.GetFiles(folder));
        Assert.Equal(Path.Combine(folder, Stem + ".mp3"), result.RecordingFilePath);
    }

    [Fact]
    public void The_partial_file_of_a_Finalization_killed_midway_is_replaced_when_the_Working_file_is_finalized_again()
    {
        WorkingFiles.Write(WorkingFilePath, 4800, _ => (1000, 1000, -1000));
        File.WriteAllText(Path.Combine(folder, Stem + ".mp3.partial"), "half an MP3, left by a killed process");

        var result = Finalization.Run(WorkingFilePath, new CapturingEncoder(), TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, Stem + ".mp3"), result.RecordingFilePath);
        Assert.Equal(CapturingEncoder.Mp3Bytes, File.ReadAllBytes(result.RecordingFilePath));
        Assert.Equal([result.RecordingFilePath], Directory.GetFiles(folder));
    }

    [Fact]
    public void Cancelling_while_encoding_keeps_the_Working_file_and_leaves_no_Recording_file_nor_partial_file()
    {
        WorkingFiles.Write(WorkingFilePath, 4800, i => ((short)i, 0, 0));
        var before = File.ReadAllBytes(WorkingFilePath);
        using var cancellation = new CancellationTokenSource();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            Finalization.Run(WorkingFilePath, new MidwayEncoder(cancellation.Cancel), cancellation.Token));

        Assert.Equal(before, File.ReadAllBytes(WorkingFilePath));
        Assert.Equal([WorkingFilePath], Directory.GetFiles(folder));
    }

    [Fact]
    public void Cancelling_before_encoding_starts_keeps_the_Working_file_and_writes_nothing()
    {
        WorkingFiles.Write(WorkingFilePath, 4800, i => ((short)i, 0, 0));
        var encoder = new CapturingEncoder();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            Finalization.Run(WorkingFilePath, encoder, new CancellationToken(canceled: true)));

        Assert.Empty(encoder.Mix);
        Assert.Equal([WorkingFilePath], Directory.GetFiles(folder));
    }
}
