using Duettino.Engine;

namespace Duettino.Selection;

/// <summary>The endpoints a Source can use right now, and the Windows default (multimedia role) for each.</summary>
interface IDeviceCatalogue
{
    /// <summary>The active endpoints for <paramref name="source"/>: capture endpoints for the Input, playback ones for the Output.</summary>
    IReadOnlyList<AudioEndpoint> Active(Source source);

    /// <summary>The Windows default endpoint for <paramref name="source"/>, or null when there is none.</summary>
    string? DefaultId(Source source);
}
