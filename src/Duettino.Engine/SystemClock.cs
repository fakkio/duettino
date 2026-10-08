using System.Diagnostics;

namespace Duettino.Engine;

/// <summary>The real clock: the performance counter (QPC) in 100 ns ticks, the unit WASAPI stamps captured packets in.</summary>
public sealed class SystemClock : IClock
{
    public long Now
    {
        get
        {
            long counter = Stopwatch.GetTimestamp();
            long frequency = Stopwatch.Frequency;
            return counter / frequency * TimeSpan.TicksPerSecond + counter % frequency * TimeSpan.TicksPerSecond / frequency;
        }
    }

    public DateTime LocalNow => DateTime.Now;
}
