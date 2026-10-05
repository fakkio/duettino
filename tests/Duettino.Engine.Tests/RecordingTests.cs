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

    [Fact]
    public void An_Input_lost_for_a_while_then_back_on_another_device_leaves_silence_in_between_and_the_Output_untouched()
    {
        // The headset microphone goes at 400 ms; at 1000 ms the Input is back, on a 44.1 kHz mono Fallback.
        using var recording = Start();
        for (int ms = 0; ms < 1400; ms += 10)
        {
            DeliverTone(recording, Source.Output, Packets.StereoFloat, toneStartMs: 0, packetMs: [ms], hz: 440);
            if (ms < 400) DeliverTone(recording, Source.Input, Packets.StereoFloat, toneStartMs: 0, packetMs: [ms], hz: 440);
            if (ms >= 1000) DeliverTone(recording, Source.Input, new SourceFormat(44100, 1, SampleType.Float32), toneStartMs: 0, packetMs: [ms], hz: 440);
        }
        RunUntil(recording, 1600);

        var file = StopAndRead(recording);

        AssertTone(file.Input, 3, 397, toneStartMs: 0, hz: 440);
        Assert.All(Enumerable.Range(400 * 48 + 1, 600 * 48 - 2), i => Assert.Equal(0, file.Input(i)));
        AssertTone(file.Input, 1003, 1397, toneStartMs: 0, hz: 440);
        AssertTone(file.OutputLeft, 3, 1397, toneStartMs: 0, hz: 440);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void Audio_Windows_reports_lost_is_padded_exactly_leaving_no_offset_between_the_Sources(int lossMs)
    {
        // Both Sources deliver every frame's own index, in 10 ms packets; at 1 s the Input loses lossMs of frames,
        // and the packet after the loss comes flagged with a data discontinuity.
        static float Mark(int frame) => (frame % 30000 - 15000) / 32768f;
        int lossStart = Rate, lossEnd = Rate + lossMs * 48;
        using var recording = Start();
        for (int first = 0; first < 2 * Rate; first += 480)
        {
            RunUntil(recording, first / 48.0 + 15);
            recording.Deliver(Source.Output, Packets.StereoFloat, Packets.Float(480, 2, (i, _) => Mark(first + i)), At(first / 48.0));
            int from = first >= lossStart && first < lossEnd ? lossEnd : first;
            if (from >= first + 480) continue;
            var flags = from == lossEnd ? PacketFlags.DataDiscontinuity : PacketFlags.None;
            recording.Deliver(Source.Input, Packets.MonoFloat, Packets.Float(first + 480 - from, 1, (i, _) => Mark(from + i)), At(from / 48.0), flags);
        }

        var file = StopAndRead(recording);

        Assert.All(Enumerable.Range(0, 2 * Rate), i =>
        {
            int expected = i % 30000 - 15000;
            Assert.InRange(file.OutputLeft(i), expected - 1, expected + 1);
            if (i >= lossStart && i < lossEnd) Assert.Equal(0, file.Input(i));
            else Assert.InRange(file.Input(i), expected - 1, expected + 1);
        });
    }

    [Fact]
    public void A_Source_running_ahead_of_the_clock_has_its_audio_dropped_beyond_two_seconds_ahead()
    {
        // An Output delivering 20 ms of a tone every 10 ms, stamped by its frame count, so it runs further and
        // further ahead of the clock. The packet delivered at 10k + 15 ms covers 20k to 20k + 20 ms, while the
        // Working file is written up to 10k - 235 ms: only what lies up to 2 s beyond that is kept, until ~3.5 s.
        using var recording = Start();
        for (int k = 0; k < 600; k++)
        {
            RunUntil(recording, 10 * k + 15);
            recording.Deliver(Source.Output, Packets.StereoFloat, Packets.Float(960, 2, (i, _) => (float)(0.5 * Math.Sin(2 * Math.PI * 440 * (960 * k + i) / Rate))), At(20 * k));
        }

        var file = StopAndRead(recording);

        Assert.Equal(6005 * 48, file.Frames);
        AssertTone(file.OutputLeft, 0, 3400, toneStartMs: 0, hz: 440);
        Assert.All(Enumerable.Range(3600 * 48, file.Frames - 3600 * 48), i => Assert.Equal(0, file.OutputLeft(i)));
    }

    const double DriftingToneHz = 70;

    [Theory]
    [InlineData(Source.Output, 44100, 300)]
    [InlineData(Source.Output, 44100, -300)]
    [InlineData(Source.Input, 48000, 100)]
    [InlineData(Source.Input, 48000, -100)]
    public void A_Source_whose_clock_drifts_from_its_timestamps_is_recorded_unbroken_and_on_time(Source source, int rate, int ppm)
    {
        var channel = RecordDriftingTone(source, rate, ppm, seconds: 120);

        // The tone, as its timestamps place it on the timeline.
        double warp = 1 + ppm * 1e-6, hz = DriftingToneHz / warp, endMs = 120_000 * warp;
        AssertContinuousTone(channel, 3, endMs - 3, hz);
        for (int s = 0; s + 1 < endMs / 1000; s++)
            Assert.InRange(ToneOffsetMs(channel, s * 1000, s * 1000 + 1000, hz), s < 30 ? -2.5 : -1.5, s < 30 ? 2.5 : 1.5);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(25)] // not a multiple of the 70 Hz period, which the offset is measured modulo
    public void Audio_lost_from_a_drifting_Source_is_padded_exactly_and_not_taken_back_as_drift(int lossMs)
    {
        const int Ppm = 300;
        var channel = RecordDriftingTone(Source.Output, 44100, Ppm, seconds: 90, lossAtMs: 60_000, lossMs);

        double warp = 1 + Ppm * 1e-6, hz = DriftingToneHz / warp, endMs = 90_000 * warp;
        double lossStartMs = 60_000 * warp, lossEndMs = (60_000 + lossMs) * warp;
        AssertContinuousTone(channel, 3, lossStartMs - 2, hz);
        Assert.All(Enumerable.Range((int)((lossStartMs + 2) * 48), (int)((lossEndMs - lossStartMs - 4) * 48)), i => Assert.Equal(0, channel(i)));
        AssertContinuousTone(channel, lossEndMs + 2, endMs - 3, hz);
        // On time on both sides of the loss: after it, as before it, not lossMs late.
        for (int s = 30; s + 1 < endMs / 1000; s++)
            if (s != 60) Assert.InRange(ToneOffsetMs(channel, s * 1000, s * 1000 + 1000, hz), -1.5, 1.5);
    }

    /// <summary>
    /// Records a Source whose data runs <paramref name="ppm"/> off its capture timestamps, as a virtual device does
    /// (+256 ppm was measured): <paramref name="seconds"/> of a 70 Hz tone in 10 ms packets, each frame stamped
    /// (1 + ppm) later than its count says. Optionally the frames from <paramref name="lossAtMs"/> on, by their count,
    /// are lost for <paramref name="lossMs"/>, the packet after the loss flagged with a data discontinuity.
    /// Returns the Source's channel (the left one for the Output) in the Working file.
    /// </summary>
    Func<int, short> RecordDriftingTone(Source source, int rate, int ppm, int seconds, int lossAtMs = 0, int lossMs = 0)
    {
        double warp = 1 + ppm * 1e-6;
        var format = new SourceFormat(rate, source == Source.Input ? 1 : 2, SampleType.Float32);
        int packetFrames = rate / 100;
        long lossStart = (long)lossAtMs * rate / 1000, lossEnd = lossStart + (long)lossMs * rate / 1000;
        using var recording = Start();
        for (long first = 0; first < (long)seconds * rate; first += packetFrames)
        {
            double ms = first * 1000.0 / rate * warp;
            RunUntil(recording, ms + 15);
            long from = first >= lossStart && first < lossEnd ? lossEnd : first;
            if (from >= first + packetFrames) continue;
            var flags = lossMs > 0 && from == lossEnd ? PacketFlags.DataDiscontinuity : PacketFlags.None;
            var packet = Packets.Encode(format, (int)(first + packetFrames - from), (i, _) => (float)(0.5 * Math.Sin(2 * Math.PI * DriftingToneHz * (from + i) / rate)));
            recording.Deliver(source, format, packet, At(from * 1000.0 / rate * warp), flags);
        }

        var file = StopAndRead(recording);
        return source == Source.Input ? file.Input : file.OutputLeft;
    }

    /// <summary>
    /// Asserts that <paramref name="channel"/> holds one unbroken tone from <paramref name="fromMs"/> to
    /// <paramref name="toMs"/>: each frame follows from its two neighbours as a sine's does. Silence cut in, or audio
    /// skipped, for even a few frames breaks that by hundreds.
    /// </summary>
    static void AssertContinuousTone(Func<int, short> channel, double fromMs, double toMs, double hz)
    {
        double twoCos = 2 * Math.Cos(2 * Math.PI * hz / Rate);
        for (int i = (int)(fromMs * 48) + 1; i < (int)(toMs * 48) - 1; i++)
        {
            double residual = channel(i + 1) + channel(i - 1) - twoCos * channel(i);
            if (Math.Abs(residual) > 30) Assert.Fail($"The tone breaks at {i / 48.0:F2} ms (residual {residual:F0}).");
        }
    }

    /// <summary>
    /// How late the tone in <paramref name="channel"/> runs, between <paramref name="fromMs"/> and <paramref name="toMs"/>,
    /// behind a sine of <paramref name="hz"/> starting at the Recording's start: within half a period either way.
    /// </summary>
    static double ToneOffsetMs(Func<int, short> channel, double fromMs, double toMs, double hz)
    {
        double omega = 2 * Math.PI * hz / Rate, sin = 0, cos = 0;
        for (int i = (int)(fromMs * 48); i < (int)(toMs * 48); i++)
        {
            sin += channel(i) * Math.Sin(omega * i);
            cos += channel(i) * Math.Cos(omega * i);
        }
        // A tone late by d reads as sin(ω(i - d)) = sin(ωi)cos(ωd) - cos(ωi)sin(ωd).
        return Math.Atan2(-cos, sin) / omega / 48;
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

    [Fact]
    public void Each_Source_reports_the_peak_of_what_it_delivered_since_the_last_reading()
    {
        using var recording = Start();
        DeliverTone(recording, Source.Input, Packets.MonoFloat, 0, [0, 10], 440, amplitude: 0.5);
        DeliverTone(recording, Source.Output, Packets.StereoFloat, 20, [0], 440, amplitude: 0.25);

        Assert.InRange(recording.TakePeak(Source.Input), 0.49f, 0.5f);
        Assert.InRange(recording.TakePeak(Source.Output), 0.24f, 0.25f);

        // Nothing delivered since: the meters fall to silence.
        Assert.Equal(0f, recording.TakePeak(Source.Input));
        Assert.Equal(0f, recording.TakePeak(Source.Output));
    }

    [Fact]
    public void The_elapsed_time_follows_the_clock_and_stops_at_Stop()
    {
        using var recording = Start();
        Assert.Equal(TimeSpan.Zero, recording.Elapsed);

        RunUntil(recording, 1500);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), recording.Elapsed);

        recording.Stop();
        clock.AdvanceMs(SafetyLatencyMs);
        recording.Advance();
        Assert.Equal(TimeSpan.FromMilliseconds(1500), recording.Elapsed);
    }
}
