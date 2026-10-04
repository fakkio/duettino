using Duettino.Engine;

namespace Duettino.Selection;

/// <summary>
/// Keeps one Source on the right endpoint while a Recording runs, as devices come and go: on a Source loss it falls
/// back to the Windows default, follows the default while in Fallback, and returns to the chosen endpoint when it is
/// back; with no device at all it uses none, and the Source is recorded as silence until one comes.
/// </summary>
/// <remarks>
/// Nothing switches while Windows is still reporting a change to the Source's own devices: a plug or unplug comes as a
/// burst of notifications, and a Bluetooth device flaps between states for seconds while it reconnects; the other
/// Source's devices don't count (a headset's microphone goes a second after its headphones). A Source switches once the
/// burst is over
/// (<see cref="SettleTime"/>) when its endpoint is gone, recording silence meanwhile, or when it is in Fallback and the
/// Windows default, so the sound, has moved elsewhere: a Fallback is only worth keeping while it is where the sound
/// goes. Otherwise it switches only once devices have been quiet for <see cref="StableTime"/>: a chosen endpoint
/// active again for a moment, while Windows still sends the sound to the Fallback, never causes a storm of switches.
/// Not thread-safe: use it from one thread, the one <see cref="IDeviceCatalogue.Changed"/> is raised on.
/// </remarks>
sealed class SourceFollower : IDisposable
{
    /// <summary>
    /// How long devices must be quiet before a Source leaves an endpoint that is gone, or a Fallback the sound left.
    /// </summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    /// <summary>How long devices must be quiet before a Source leaves an endpoint that still carries the sound.</summary>
    public static readonly TimeSpan StableTime = TimeSpan.FromSeconds(2);

    /// <summary>How long after a capture is lost the same endpoint is opened again, if Windows still lists it.</summary>
    public static readonly TimeSpan RetryTime = TimeSpan.FromSeconds(2);

    readonly IDeviceCatalogue devices;
    readonly IClock clock;
    readonly Source source;
    readonly AudioEndpoint? chosen;
    long? lastChange; // when the Source's devices last changed, or its capture was lost, not acted on since
    string view; // the Source's active endpoints and default, as last seen
    bool lost;

    /// <summary>Starts on the endpoint <see cref="SourceSelection.For"/> selects for <paramref name="chosen"/>.</summary>
    public SourceFollower(IDeviceCatalogue devices, IClock clock, Source source, AudioEndpoint? chosen)
    {
        (this.devices, this.clock, this.source, this.chosen) = (devices, clock, source, chosen);
        Selection = SourceSelection.For(devices, source, chosen);
        view = View();
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
    /// Reads the catalogue again if devices changed and it is time to; call it regularly (the window does each time it
    /// moves the meters). Returns true when the Source's capture must be opened afresh on
    /// <see cref="SourceSelection.InUse"/>, or closed if it is none.
    /// </summary>
    public bool Poll()
    {
        if (lastChange is not { } changed) return false;
        var quiet = clock.Now - changed;
        if (quiet < SettleTime.Ticks) return false;

        var next = SourceSelection.For(devices, source, chosen);
        bool same = next.InUse?.Id == Selection.InUse?.Id;
        bool works = !lost && next.Endpoints.Any(e => e.Id == Selection.InUse?.Id);
        bool soundMoved = Selection.IsFallback && next.InUse?.Id == devices.DefaultId(source);
        // Lost on an endpoint Windows still lists: it may not be ready yet, so it is tried again, not hammered.
        if (same && lost && next.InUse != null && quiet < RetryTime.Ticks) return false;
        if (!same && works && !soundMoved && quiet < StableTime.Ticks) return false;

        bool reopen = !same || (lost && next.InUse != null);
        Selection = next;
        lastChange = null;
        lost = false;
        return reopen;
    }

    public void Dispose() => devices.Changed -= OnChanged;

    /// <summary>Only a change to this Source's own endpoints or default counts: the other Source's don't hold it back.</summary>
    void OnChanged()
    {
        var now = View();
        if (now == view) return;
        view = now;
        lastChange = clock.Now;
    }

    string View() => string.Join("|", devices.Active(source).Select(e => e.Id)) + "/" + devices.DefaultId(source);
}
