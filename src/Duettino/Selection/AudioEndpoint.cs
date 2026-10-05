namespace Duettino.Selection;

/// <summary>An audio endpoint, as Windows names it.</summary>
sealed record AudioEndpoint(string Id, string Name)
{
    public override string ToString() => Name;
}
