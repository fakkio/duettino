using Duettino.Engine;

namespace Duettino.Selection;

/// <summary>
/// Keeps one Source on the right endpoint while a Recording runs, as devices come and go: on a Source loss it falls
/// back to the Windows default, follows the default while in Fallback, and returns to the chosen endpoint when it is
/// back; with no device at all it uses none, and the Source is recorded as silence until one comes.
/// </summary>
/// <remarks>
/// Nothing switches while Windows is still reporting a change: a plug or unplug comes as a burst of notifications, and
/// a Bluetooth device flaps between states for seconds while it reconnects. A Source whose endpoint is gone switches
/// once the burst is over (<see cref="SettleTime"/>): it is recording silence meanwhile. One whose endpoint still works
/// switches only once devices have been quiet for <see cref="StableTime"/>, so flapping never causes a storm of switches.
/// Not thread-safe: use it from one thread, the one <see cref="IDeviceCatalogue.Changed"/> is raised on.
/// </remarks>
sealed class SourceFollower : IDisposable
{
    /// <summary>How long devices must be quiet before a Source whose endpoint is gone switches.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    /// <summary>How long devices must be quiet before a Source whose endpoint still works switches.</summary>
    public static readonly TimeSpan StableTime = TimeSpan.FromSeconds(2);

    /// <summary>How long after a capture is lost the same endpoint is opened again, if Windows still lists it.</summary>
    public static readonly TimeSpan RetryTime = TimeSpan.FromSeconds(2);

    readonly IDeviceCatalogue devices;
    readonly IClock clock;
    readonly Source source;
    readonly AudioEndpoint? chosen;
    long? lastChange; // when devices last changed, or the capture was lost, with the catalogue not read again since
    bool lost;

    /// <summary>Starts on the endpoint <see cref="SourceSelection.For"/> selects for <paramref name="chosen"/>.</summary>
    public SourceFollower(IDeviceCatalogue devices, IClock clock, Source source, AudioEndpoint? chosen)
    {
        (this.devices, this.clock, this.source, this.chosen) = (devices, clock, source, chosen);
        Selection = SourceSelection.For(devices, source, chosen);
        devices.Changed += OnChanged;
    }

    /// <summary>The endpoint the Source records from (none: silence), out of the active ones.</summary>
    public SourceSelection Selection { get; private set; }

    /// <summary>
    /// The capture on <see cref="SourceSelection.InUse"/> stopped or couldn't be opened: its device is gone, or not
    /// ready. The Source is recorded as silence until <see cref="Poll"/> says to open a capture again.
    /// </summary>
    /// <summary>True from <see cref="Lost"/> until <see cref="Poll"/> says to open a capture again, or to use none.</summary>
    public bool IsLost => lost;

    public void Lost()
    {
        lost = true;
        lastChange = clock.Now;
    }

    /// <summary>
    /// Reads the catalogue again if devices changed and it is time to; call it regularly (the window does as often as it moves the meters).
    /// Returns true when the Source's capture must be opened afresh on <see cref="SourceSelection.InUse"/>, or closed
    /// if it is none.
    /// </summary>
    public bool Poll()
    {
        if (lastChange is not { } changed) return false;
        var quiet = clock.Now - changed;
        if (quiet < SettleTime.Ticks) return false;

        var next = SourceSelection.For(devices, source, chosen);
        bool same = next.InUse?.Id == Selection.InUse?.Id;
        bool works = !lost && next.Endpoints.Any(e => e.Id == Selection.InUse?.Id);
        // Lost on an endpoint Windows still lists: it may not be ready yet, so it is tried again, not hammered.
        if (same && lost && next.InUse != null && quiet < RetryTime.Ticks) return false;
        if (!same && works && quiet < StableTime.Ticks) return false;

        bool reopen = !same || (lost && next.InUse != null);
        Selection = next;
        lastChange = null;
        lost = false;
        return reopen;
    }

    public void Dispose() => devices.Changed -= OnChanged;

    void OnChanged() => lastChange = clock.Now;
}
