using NAudio.Wave;

namespace Duettino.Engine.Tests;

public sealed class RecordingTests : IDisposable
{
    const int Rate = 48000;
    const int SafetyLatencyMs = 250;

    readonly string folder = Path.Combine(Path.GetTempPath(), "duettino-tests", Guid.NewGuid().ToString("N"));
    readonly FakeClock clock = new(new DateTime(2026, 10, 1, 14, 30, 5));
    long startTicks;

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    Recording Start()
    {
        startTicks = clock.Now;
        return Recording.Start(folder, clock);
    }

    /// <summary>The capture timestamp of a frame <paramref name="ms"/> after the Recording started.</summary>
    long At(double ms) => startTicks + (long)Math.Round(ms * TimeSpan.TicksPerSecond / 1000);

    /// <summary>Moves the clock to <paramref name="ms"/> after the start in 10 ms steps, writing as a real pacer would.</summary>
    void RunUntil(Recording recording, double ms)
    {
        while (clock.Now < At(ms))
        {
            clock.AdvanceMs(Math.Min(10, (At(ms) - clock.Now) * 1000.0 / TimeSpan.TicksPerSecond));
            recording.Advance();
        }
    }

    /// <summary>Stops the Recording and lets the clock run past the safety latency, so the Working file is complete.</summary>
    WorkingFile StopAndRead(Recording recording)
    {
        recording.Stop();
        clock.AdvanceMs(SafetyLatencyMs);
        recording.Advance();
        Assert.True(recording.IsCompleted);
        return WorkingFile.Read(recording.WorkingFilePath);
    }

    /// <summary>
    /// Delivers a tone, the same on every channel, in 10 ms packets as a Source would: the packet captured
    /// <c>ms</c> after <paramref name="toneStartMs"/> arrives 15 ms later, for each <c>ms</c> in <paramref name="packetMs"/>.
    /// </summary>
    void DeliverTone(Recording recording, Source source, SourceFormat format, double toneStartMs, IEnumerable<int> packetMs, double hz, double amplitude = 0.5)
    {
        int rate = format.SampleRate, packetFrames = rate / 100;
        foreach (int ms in packetMs)
        {
            RunUntil(recording, toneStartMs + ms + 15);
            long first = (long)ms * rate / 1000;
            var packet = Packets.Encode(format, packetFrames, (i, _) => (float)(amplitude * Math.Sin(2 * Math.PI * hz * (first + i) / rate)));
            recording.Deliver(source, format, packet, At(toneStartMs + ms));
        }
    }

    /// <summary>
    /// Asserts that <paramref name="channel"/> holds, frame by frame from <paramref name="fromMs"/> to
    /// <paramref name="toMs"/>, the tone <see cref="DeliverTone"/> started at <paramref name="toneStartMs"/>.
    /// Off by one frame, a 440 Hz tone would be off by ~950.
    /// </summary>
    static void AssertTone(Func<int, short> channel, double fromMs, double toMs, double toneStartMs, double hz, double amplitude = 0.5) =>
        Assert.All(Enumerable.Range((int)(fromMs * 48), (int)((toMs - fromMs) * 48)), i =>
        {
            double expected = amplitude * Math.Sin(2 * Math.PI * hz * (i - toneStartMs * 48) / Rate) * short.MaxValue;
            Assert.InRange(channel(i), expected - 160, expected + 160);
        });

    [Fact]
    public void Sources_sending_no_packets_at_all_yield_a_silent_Working_file_as_long_as_the_clock_ran()
    {
        using var recording = Start();
        RunUntil(recording, 2000);

        var file = StopAndRead(recording);

        Assert.Equal(Path.Combine(folder, "Duettino_2026-10-01_14-30-05.working.wav"), recording.WorkingFilePath);
        Assert.Equal((Rate, 16, 3), (file.Format.SampleRate, file.Format.BitsPerSample, file.Format.Channels));
        Assert.Equal(2 * Rate, file.Frames);
        Assert.All(file.Samples, s => Assert.Equal(0, s));
    }

    [Fact]
    public void An_Input_packet_lands_at_its_capture_timestamp()
    {
        using var recording = Start();
        RunUntil(recording, 400);
        recording.Deliver(Source.Input, Packets.MonoFloat, Packets.Float(4800, 1, (_, _) => 0.5f), At(300));
        RunUntil(recording, 1000);

        var file = StopAndRead(recording);

        Assert.Equal(0, file.Input(14399));
        Assert.Equal(16384, file.Input(14400));
        Assert.Equal(16384, file.Input(19199));
        Assert.Equal(0, file.Input(19200));
        Assert.All(Enumerable.Range(0, file.Frames), i => Assert.Equal((0, 0), (file.OutputLeft(i), file.OutputRight(i))));
    }

    [Fact]
    public void Sources_whose_first_packets_arrive_at_different_times_stay_aligned()
    {
        using var recording = Start();
        var silence = Packets.Float(480, 2, (_, _) => 0f);
        var click = Packets.Float(480, 2, (i, c) => i == 0 ? (c == 0 ? 0.5f : -0.5f) : 0f);

        // The Input starts delivering 30 ms after Record, the Output 450 ms after; both hear a click at 600 ms.
        RunUntil(recording, 40);
        recording.Deliver(Source.Input, Packets.StereoFloat, silence, At(30));
        RunUntil(recording, 460);
        recording.Deliver(Source.Output, Packets.StereoFloat, silence, At(450));
        RunUntil(recording, 610);
        recording.Deliver(Source.Input, Packets.StereoFloat, click, At(600));
        recording.Deliver(Source.Output, Packets.StereoFloat, click, At(600));
        RunUntil(recording, 1000);

        var file = StopAndRead(recording);

        Assert.Equal(0, file.Input(28799)); // stereo Input: the two channels averaged to mono
        Assert.Equal((16384, -16384), (file.OutputLeft(28800), file.OutputRight(28800)));
        Assert.Equal(1, Enumerable.Range(0, file.Frames).Count(i => file.OutputLeft(i) != 0));
    }

    [Fact]
    public void An_Output_sending_packets_of_zeros_is_recorded_as_silence_alongside_the_Input()
    {
        using var recording = Start();
        var tone = Packets.Float(480, 1, (i, _) => i % 2 == 0 ? 0.25f : -0.25f);
        var zeros = Packets.Float(480, 2, (_, _) => 0f);
        for (int ms = 0; ms < 1500; ms += 10)
        {
            RunUntil(recording, ms + 10);
            recording.Deliver(Source.Input, Packets.MonoFloat, tone, At(ms));
            recording.Deliver(Source.Output, Packets.StereoFloat, zeros, At(ms));
        }

        var file = StopAndRead(recording);

        Assert.Equal(72000, file.Frames);
        Assert.All(Enumerable.Range(0, file.Frames), i => Assert.Equal(i % 2 == 0 ? 8192 : -8192, file.Input(i)));
        Assert.All(Enumerable.Range(0, file.Frames), i => Assert.Equal((0, 0), (file.OutputLeft(i), file.OutputRight(i))));
    }

    [Fact]
    public void Packets_arriving_late_or_in_bursts_are_placed_by_their_capture_timestamp()
    {
        // Every frame carries its own index, so any misplacement shows. Over 0..3 s, packets of 10 ms each:
        // the Input arrives 5 ms after capture, except for a 100 ms stall at 1 s released in one burst;
        // the Output arrives 200 ms after capture, except for a 100 ms stall at 2 s (worst packet 230 ms late,
        // still inside the 250 ms safety latency).
        static float Mark(int frame) => (frame % 30000 - 15000) / 32768f;
        var arrivals = new List<(double ArrivalMs, Source Source, int FirstFrame)>();
        for (int ms = 0; ms < 3000; ms += 10)
        {
            double inputArrival = ms is >= 1000 and < 1100 ? 1110 : ms + 15;
            double outputArrival = ms is >= 2000 and < 2100 ? 2240 : ms + 210;
            arrivals.Add((inputArrival, Source.Input, ms * 48));
            arrivals.Add((outputArrival, Source.Output, ms * 48));
        }

        using var recording = Start();
        foreach (var (arrivalMs, source, firstFrame) in arrivals.OrderBy(a => a.ArrivalMs))
        {
            RunUntil(recording, arrivalMs);
            var format = source == Source.Input ? Packets.MonoFloat : Packets.StereoFloat;
            recording.Deliver(source, format, Packets.Float(480, format.Channels, (i, _) => Mark(firstFrame + i)), At(firstFrame / 48.0));
        }

        var file = StopAndRead(recording); // the last Output packet arrived at 3.2 s

        Assert.Equal(3.2 * Rate, file.Frames);
        Assert.All(Enumerable.Range(0, 3 * Rate), i =>
        {
            int expected = i % 30000 - 15000;
            Assert.InRange(file.Input(i), expected - 1, expected + 1);
            Assert.InRange(file.OutputLeft(i), expected - 1, expected + 1);
            Assert.InRange(file.OutputRight(i), expected - 1, expected + 1);
        });
    }

    [Fact]
    public void Jittery_timestamps_and_empty_packets_with_bogus_timestamps_leave_the_audio_seamless()
    {
        // As a real Realtek endpoint through NAudio: 10 ms packets whose timestamps wobble by a few frames,
        // each followed by an empty packet stamped 0.
        static float Mark(int frame) => (frame % 30000 - 15000) / 32768f;
        int[] wobble = [0, -4, 3, -2, 5, -5, 1];
        using var recording = Start();
        for (int k = 0; k < 150; k++)
        {
            RunUntil(recording, k * 10 + 15);
            int first = k * 480;
            recording.Deliver(Source.Input, Packets.MonoFloat, Packets.Float(480, 1, (i, _) => Mark(first + i)), At((first + wobble[k % wobble.Length]) / 48.0));
            recording.Deliver(Source.Input, Packets.MonoFloat, [], captureTime: 0);
        }

        var file = StopAndRead(recording);

        Assert.All(Enumerable.Range(0, 150 * 480), i =>
        {
            int expected = i % 30000 - 15000;
            Assert.InRange(file.Input(i), expected - 1, expected + 1);
        });
    }

    [Theory]
    [InlineData(Source.Input, 16000, 1, SampleType.Pcm16, false)]
    [InlineData(Source.Input, 16000, 1, SampleType.Pcm16, true)]
    [InlineData(Source.Input, 44100, 2, SampleType.Float32, true)]
    [InlineData(Source.Input, 44100, 1, SampleType.Pcm16, false)]
    [InlineData(Source.Input, 48000, 2, SampleType.Pcm16, true)]
    [InlineData(Source.Input, 48000, 6, SampleType.Float32, true)]
    [InlineData(Source.Input, 16000, 6, SampleType.Pcm16, false)]
    [InlineData(Source.Output, 48000, 2, SampleType.Pcm16, false)]
    [InlineData(Source.Output, 48000, 1, SampleType.Float32, false)]
    [InlineData(Source.Output, 44100, 2, SampleType.Float32, true)]
    [InlineData(Source.Output, 44100, 2, SampleType.Float32, false)]
    [InlineData(Source.Output, 16000, 2, SampleType.Pcm16, true)]
    [InlineData(Source.Output, 96000, 2, SampleType.Float32, true)]
    [InlineData(Source.Output, 44100, 6, SampleType.Float32, true)]
    public void A_Source_in_any_format_is_recorded_at_48_kHz_with_its_content_and_duration(
        Source source, int rate, int channels, SampleType type, bool extensible)
    {
        // One second of a 440 Hz tone, the same on every channel, from 100 ms on.
        var format = SourceFormat.From(WaveFormats.Create(rate, channels, type, extensible));
        using var recording = Start();
        DeliverTone(recording, source, format, toneStartMs: 100, packetMs: Enumerable.Range(0, 100).Select(k => 10 * k), hz: 440);
        RunUntil(recording, 1500);

        var file = StopAndRead(recording);

        // The first and last 3 ms are left out, where the tone starts and stops abruptly.
        Func<int, short>[] channelsOf = source == Source.Input ? [file.Input] : [file.OutputLeft, file.OutputRight];
        foreach (var channel in channelsOf)
        {
            Assert.All(Enumerable.Range(0, file.Frames).Where(i => i < 100 * 48 - 1 || i > 1100 * 48 + 1), i => Assert.Equal(0, channel(i)));
            AssertTone(channel, 103, 1097, toneStartMs: 100, hz: 440);
        }
        Func<int, short>[] silent = source == Source.Input ? [file.OutputLeft, file.OutputRight] : [file.Input];
        Assert.All(silent, channel => Assert.All(Enumerable.Range(0, file.Frames), i => Assert.Equal(0, channel(i))));
    }

    [Theory]
    [InlineData(true, Speakers.FrontLeft | Speakers.FrontRight | Speakers.FrontCenter | Speakers.LowFrequency | Speakers.BackLeft | Speakers.BackRight)]
    [InlineData(true, Speakers.FrontLeft | Speakers.FrontRight | Speakers.FrontCenter | Speakers.LowFrequency | Speakers.SideLeft | Speakers.SideRight)]
    [InlineData(false, Speakers.None)] // a plain format: Windows' default 5.1 layout
    public void A_5_1_Output_is_downmixed_to_stereo_keeping_the_center_and_surrounds_and_dropping_the_LFE_without_clipping(bool extensible, Speakers layout)
    {
        // A steady level per speaker: front left, front right, center, LFE, surround left, surround right.
        float[] levels = [0.1f, 0.2f, 0.3f, 0.4f, 0.05f, -0.05f];
        var wave = extensible
            ? new WaveFormatExtensible(48000, 32, 6, AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT, 32, layout)
            : WaveFormat.CreateIeeeFloatWaveFormat(48000, 6);
        var format = SourceFormat.From(wave);
        using var recording = Start();
        RunUntil(recording, 110);
        recording.Deliver(Source.Output, format, Packets.Encode(format, 4800, (_, c) => levels[c]), At(100));
        RunUntil(recording, 500);

        var file = StopAndRead(recording);

        // ITU-R BS.775: center and surrounds at -3 dB, LFE left out; scaled so that a side can't exceed full scale.
        const double Minus3dB = 0.7071, Scale = 1 / (1 + Minus3dB + Minus3dB);
        double left = Scale * (0.1 + Minus3dB * 0.3 + Minus3dB * 0.05), right = Scale * (0.2 + Minus3dB * 0.3 - Minus3dB * 0.05);
        Assert.InRange(file.OutputLeft(150 * 48), left * short.MaxValue - 2, left * short.MaxValue + 2);
        Assert.InRange(file.OutputRight(150 * 48), right * short.MaxValue - 2, right * short.MaxValue + 2);
    }

    [Fact]
    public void Sound_above_what_48_kHz_can_hold_is_filtered_out_rather_than_folded_into_audible_frequencies()
    {
        // A 96 kHz Output playing a 30 kHz tone: sampled naively at 48 kHz it would come back as an 18 kHz one.
        using var recording = Start();
        DeliverTone(recording, Source.Output, new SourceFormat(96000, 2, SampleType.Float32), toneStartMs: 100, packetMs: Enumerable.Range(0, 50).Select(k => 10 * k), hz: 30000);
        RunUntil(recording, 800);

        var file = StopAndRead(recording);

        // Away from where the tone starts and stops, -80 dBFS at most.
        Assert.All(Enumerable.Range(103 * 48, 494 * 48), i => Assert.InRange(file.OutputLeft(i), -3, 3));
    }

    [Fact]
    public void A_resampled_Source_that_pauses_leaves_a_silent_Gap_and_resumes_at_its_capture_timestamp()
    {
        // As a loopback capture at 16 kHz while the player pauses for longer than the safety latency: a tone from
        // 100 to 400 ms, nothing until 1000 ms, then the tone again until 1300 ms.
        using var recording = Start();
        var packetMs = Enumerable.Range(0, 120).Select(k => 10 * k).Where(ms => ms is < 300 or >= 900);
        DeliverTone(recording, Source.Output, new SourceFormat(16000, 2, SampleType.Float32), toneStartMs: 100, packetMs, hz: 440);
        RunUntil(recording, 1600);

        var file = StopAndRead(recording);

        Assert.All(Enumerable.Range(400 * 48 + 1, 600 * 48 - 2), i => Assert.Equal((0, 0), (file.OutputLeft(i), file.OutputRight(i))));
        AssertTone(file.OutputLeft, 103, 397, toneStartMs: 100, hz: 440);
        AssertTone(file.OutputLeft, 1003, 1297, toneStartMs: 100, hz: 440);
        // The tone runs right up to where it paused: its last millisecond is all there, at the tone's loudness.
        Assert.InRange(Rms(file.OutputLeft, 399, 400), 0.8 * ToneRms, 1.2 * ToneRms);
    }

    [Fact]
    public void An_Output_switching_to_a_device_in_another_format_mid_Recording_stays_continuous()
    {
        // A Fallback from a 44.1 kHz headset to 48 kHz speakers at 500 ms, the tone carrying on across.
        using var recording = Start();
        DeliverTone(recording, Source.Output, new SourceFormat(44100, 2, SampleType.Float32), toneStartMs: 100, packetMs: Enumerable.Range(0, 40).Select(k => 10 * k), hz: 440);
        DeliverTone(recording, Source.Output, new SourceFormat(48000, 2, SampleType.Float32), toneStartMs: 100, packetMs: Enumerable.Range(40, 40).Select(k => 10 * k), hz: 440);
        RunUntil(recording, 1200);

        var file = StopAndRead(recording);

        AssertTone(file.OutputLeft, 103, 497, toneStartMs: 100, hz: 440);
        AssertTone(file.OutputLeft, 503, 897, toneStartMs: 100, hz: 440);
        Assert.InRange(Rms(file.OutputLeft, 497, 503), 0.8 * ToneRms, 1.2 * ToneRms);
    }

    const double ToneRms = 0.5 / 1.41421356 * short.MaxValue;

    static double Rms(Func<int, short> channel, double fromMs, double toMs) =>
        Math.Sqrt(Enumerable.Range((int)(fromMs * 48), (int)((toMs - fromMs) * 48)).Average(i => (double)channel(i) * channel(i)));

    [Fact]
    public void The_Working_file_header_is_brought_up_to_date_every_second_while_recording()
    {
        using var recording = Start();

        RunUntil(recording, 1200); // 0.95 s written
        Assert.Equal(0, WorkingFile.Read(recording.WorkingFilePath).Frames);

        RunUntil(recording, 1300); // 1.05 s written, header updated at 1 s
        Assert.Equal(48000, WorkingFile.Read(recording.WorkingFilePath).Frames);
    }
}
