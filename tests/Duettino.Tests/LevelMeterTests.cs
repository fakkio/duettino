namespace Duettino.Tests;

public sealed class LevelMeterTests
{
    [Fact]
    public void The_meter_reads_peaks_on_a_60_dB_scale()
    {
        using var meter = new LevelMeter();

        meter.ShowPeak(1f);
        Assert.Equal(1, meter.Fraction, 3);

        meter.Reset();
        meter.ShowPeak(0.1f); // -20 dBFS
        Assert.Equal(2 / 3.0, meter.Fraction, 3);

        meter.Reset();
        meter.ShowPeak(0.001f); // -60 dBFS
        Assert.Equal(0, meter.Fraction, 3);

        meter.ShowPeak(0f);
        Assert.Equal(0, meter.Fraction, 3);
    }

    [Fact]
    public void A_peak_that_stops_falls_back_gradually_to_empty_within_two_seconds()
    {
        using var meter = new LevelMeter();
        meter.ShowPeak(1f);

        meter.ShowPeak(0f);
        Assert.InRange(meter.Fraction, 0.5, 0.99);

        for (int update = 1; update < 20; update++) meter.ShowPeak(0f);
        Assert.Equal(0, meter.Fraction, 3);
    }

    [Fact]
    public void A_louder_peak_shows_at_once()
    {
        using var meter = new LevelMeter();
        meter.ShowPeak(0.001f);

        meter.ShowPeak(1f);

        Assert.Equal(1, meter.Fraction, 3);
    }

    [Fact]
    public void Reset_empties_the_meter()
    {
        using var meter = new LevelMeter();
        meter.ShowPeak(1f);

        meter.Reset();

        Assert.Equal(0, meter.Fraction, 3);
    }
}
