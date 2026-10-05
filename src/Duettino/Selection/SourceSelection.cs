using Duettino.Engine;

namespace Duettino.Selection;

/// <summary>Which endpoint a Source uses, out of the active ones, and the one the user chose for it.</summary>
sealed record SourceSelection(IReadOnlyList<AudioEndpoint> Endpoints, AudioEndpoint? Chosen, AudioEndpoint? InUse, bool IsFallback)
{
    /// <summary>
    /// The chosen endpoint if it is active, otherwise the Windows default: a Fallback if a chosen endpoint is absent,
    /// a plain preselection if nothing was chosen yet. With no device at all, none: the Source is recorded as silence.
    /// </summary>
    public static SourceSelection For(IDeviceCatalogue devices, Source source, AudioEndpoint? chosen)
    {
        var endpoints = devices.Active(source);
        if (endpoints.FirstOrDefault(e => e.Id == chosen?.Id) is { } active) return new(endpoints, chosen, active, false);

        // Windows has a default whenever an endpoint is active, but the two are read apart: one may be read mid-change.
        var defaultId = devices.DefaultId(source);
        var fallback = endpoints.FirstOrDefault(e => e.Id == defaultId) ?? endpoints.FirstOrDefault();
        return new(endpoints, chosen, fallback, chosen != null && fallback != null);
    }
}
