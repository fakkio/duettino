using NAudio.CoreAudioApi;

namespace Duettino.Devices;

/// <summary>An active audio endpoint, as Windows names it.</summary>
sealed record AudioEndpoint(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>The active endpoints a Source can use, and the Windows default (multimedia role) for each.</summary>
static class AudioDevices
{
    public static IReadOnlyList<AudioEndpoint> Inputs() => List(DataFlow.Capture);

    public static IReadOnlyList<AudioEndpoint> Outputs() => List(DataFlow.Render);

    public static string? DefaultInputId() => DefaultId(DataFlow.Capture);

    public static string? DefaultOutputId() => DefaultId(DataFlow.Render);

    static IReadOnlyList<AudioEndpoint> List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Select(d => new AudioEndpoint(d.ID, d.FriendlyName))
            .ToList();
    }

    static string? DefaultId(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.TryGetDefaultAudioEndpoint(flow, Role.Multimedia, out var device) ? device.ID : null;
    }
}
