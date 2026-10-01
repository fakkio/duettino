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
    public void The_Working_file_header_is_brought_up_to_date_every_second_while_recording()
    {
        using var recording = Start();

        RunUntil(recording, 1200); // 0.95 s written
        Assert.Equal(0, WorkingFile.Read(recording.WorkingFilePath).Frames);

        RunUntil(recording, 1300); // 1.05 s written, header updated at 1 s
        Assert.Equal(48000, WorkingFile.Read(recording.WorkingFilePath).Frames);
    }
}
