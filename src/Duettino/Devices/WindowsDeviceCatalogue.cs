using Duettino.Engine;
using Duettino.Selection;
using NAudio.CoreAudioApi;

namespace Duettino.Devices;

/// <summary>The endpoints Windows reports, and a <see cref="Changed"/> event when they come, go or the default moves.</summary>
sealed class WindowsDeviceCatalogue : IDeviceCatalogue, IDisposable
{
    readonly MMDeviceEnumerator enumerator = new();
    readonly MMDeviceNotificationClient notifications;

    /// <summary>
    /// Create it on the UI thread: <see cref="Changed"/> is raised there. Throws a COM error when the Windows audio
    /// system can't be reached (see <see cref="AudioSystem"/>).
    /// </summary>
    public WindowsDeviceCatalogue()
    {
        try
        {
            notifications = enumerator.CreateNotificationClient(useSynchronizationContext: true);
        }
        catch
        {
            enumerator.Dispose();
            throw;
        }
        notifications.DeviceAdded += (_, _) => Changed?.Invoke();
        notifications.DeviceRemoved += (_, _) => Changed?.Invoke();
        notifications.DeviceStateChanged += (_, _) => Changed?.Invoke();
        notifications.DefaultDeviceChanged += (_, e) =>
        {
            if (e.Role == Role.Multimedia) Changed?.Invoke();
        };
    }

    /// <summary>
    /// Raised on the UI thread, often several times in a burst for one plug or unplug: read the catalogue again once
    /// the burst is over.
    /// </summary>
    public event Action? Changed;

    // The catalogue is read again on every change, so each COM wrapper is released as soon as it has been read.
    public IReadOnlyList<AudioEndpoint> Active(Source source)
    {
        using var devices = enumerator.EnumerateAudioEndPoints(Flow(source), DeviceState.Active);
        var endpoints = new List<AudioEndpoint>();
        foreach (var device in devices)
            using (device)
                endpoints.Add(new AudioEndpoint(device.ID, device.FriendlyName));
        return endpoints;
    }

    public string? DefaultId(Source source)
    {
        if (!enumerator.TryGetDefaultAudioEndpoint(Flow(source), Role.Multimedia, out var device)) return null;
        using (device) return device.ID;
    }

    public void Dispose()
    {
        notifications.Dispose();
        enumerator.Dispose();
    }

    static DataFlow Flow(Source source) => source == Source.Input ? DataFlow.Capture : DataFlow.Render;
}
