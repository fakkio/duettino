using Duettino.Engine;
using Duettino.Selection;

namespace Duettino.Tests;

/// <summary>A device catalogue the test sets up by hand, standing in for the endpoints Windows reports.</summary>
sealed class FakeDeviceCatalogue : IDeviceCatalogue
{
    readonly Dictionary<Source, AudioEndpoint[]> active = new() { [Source.Input] = [], [Source.Output] = [] };
    readonly Dictionary<Source, AudioEndpoint?> defaults = new() { [Source.Input] = null, [Source.Output] = null };

    public FakeDeviceCatalogue WithInputs(params AudioEndpoint[] endpoints) => With(Source.Input, endpoints);

    public FakeDeviceCatalogue WithOutputs(params AudioEndpoint[] endpoints) => With(Source.Output, endpoints);

    public FakeDeviceCatalogue WithDefaultInput(AudioEndpoint endpoint) => WithDefault(Source.Input, endpoint);

    public FakeDeviceCatalogue WithDefaultOutput(AudioEndpoint endpoint) => WithDefault(Source.Output, endpoint);

    public IReadOnlyList<AudioEndpoint> Active(Source source) => active[source];

    public string? DefaultId(Source source) => defaults[source]?.Id;

    FakeDeviceCatalogue With(Source source, AudioEndpoint[] endpoints)
    {
        active[source] = endpoints;
        return this;
    }

    FakeDeviceCatalogue WithDefault(Source source, AudioEndpoint endpoint)
    {
        defaults[source] = endpoint;
        return this;
    }
}
