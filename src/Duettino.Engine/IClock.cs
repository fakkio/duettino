namespace Duettino.Engine;

/// <summary>The time that drives a Recording (ADR-0005).</summary>
public interface IClock
{
    /// <summary>Current time in 100 ns ticks, on the timebase of packet capture timestamps (QPC, as WASAPI reports it).</summary>
    long Now { get; }

    /// <summary>Current local wall-clock time, used to name the Recording.</summary>
    DateTime LocalNow { get; }
}
