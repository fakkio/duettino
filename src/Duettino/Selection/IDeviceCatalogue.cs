using Duettino.Engine;

namespace Duettino.Selection;

/// <summary>The endpoints a Source can use right now, and the Windows default (multimedia role) for each.</summary>
interface IDeviceCatalogue
{
    /// <summary>
    /// Raised when endpoints come, go or the default moves, often several times in a burst for one plug or unplug:
    /// read the catalogue again once the burst is over.
    /// </summary>
    event Action? Changed;

    /// <summary>The active endpoints for <paramref name="source"/>: capture endpoints for the Input, playback ones for the Output.</summary>
    IReadOnlyList<AudioEndpoint> Active(Source source);

    /// <summary>The Windows default endpoint for <paramref name="source"/>, or null when there is none.</summary>
    string? DefaultId(Source source);
}
