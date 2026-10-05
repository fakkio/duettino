using Duettino.Engine;

namespace Duettino.Tests;

/// <summary>A clock the test moves by hand.</summary>
sealed class FakeClock : IClock
{
    // Arbitrary, non-zero, so tests don't pass by assuming the timebase starts at 0.
    public long Now { get; private set; } = 123_456_789_000;

    public DateTime LocalNow => new DateTime(2026, 10, 4, 10, 0, 0).AddTicks(Now);

    public void AdvanceMs(double ms) => Now += (long)Math.Round(ms * TimeSpan.TicksPerMillisecond);
}
