namespace Duettino;

/// <summary>
/// A Source's level meter: a bar on a 60 dB scale that jumps up to each new peak and falls back gradually, so speech
/// reads as a steady movement rather than a flicker.
/// </summary>
sealed class LevelMeter : Control
{
    /// <summary>How often <see cref="ShowPeak"/> is meant to be called: about 10 times per second.</summary>
    public const int RefreshIntervalMs = 100;

    const double RangeDb = 60;

    /// <summary>How far the bar falls at each <see cref="ShowPeak"/> without a louder peak: empty from full scale in 2 s.</summary>
    const double FallDbPerShow = 60.0 / (2000 / RefreshIntervalMs);

    /// <summary>A peak this close to full scale turns the bar red: the Source is about to clip.</summary>
    const double HotDb = -1;

    double levelDb = -RangeDb;

    public LevelMeter()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    /// <summary>How much of the bar is filled, from 0 (-60 dBFS or below) to 1 (full scale).</summary>
    public double Fraction => (levelDb + RangeDb) / RangeDb;

    /// <summary>Shows the latest <paramref name="peak"/> (full scale is 1); call it every <see cref="RefreshIntervalMs"/>.</summary>
    public void ShowPeak(float peak)
    {
        double peakDb = peak > 0 ? Math.Clamp(20 * Math.Log10(peak), -RangeDb, 0) : -RangeDb;
        Set(Math.Max(peakDb, levelDb - FallDbPerShow));
    }

    /// <summary>Empties the bar at once.</summary>
    public void Reset() => Set(-RangeDb);

    void Set(double db)
    {
        if (db == levelDb) return;
        levelDb = db;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        using var track = new SolidBrush(SystemColors.ControlLight);
        e.Graphics.FillRectangle(track, bounds);
        int filled = (int)Math.Round(bounds.Width * Fraction);
        if (filled == 0) return;
        using var bar = new SolidBrush(levelDb >= HotDb ? Color.Firebrick : Color.SeaGreen);
        e.Graphics.FillRectangle(bar, bounds with { Width = filled });
    }
}
