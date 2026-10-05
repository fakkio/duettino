namespace Duettino.Devices;

/// <summary>What an error opening or running a capture means for its Source.</summary>
enum CaptureError
{
    /// <summary>The device is gone, or not available right now: the Source falls back, or waits for it.</summary>
    Unavailable,

    /// <summary>Windows privacy settings don't let desktop apps use the microphone.</summary>
    AccessDenied,

    /// <summary>Anything else: the Recording can't go on.</summary>
    Unrecoverable,
}

static class CaptureErrors
{
    const int AccessDenied = unchecked((int)0x80070005); // E_ACCESSDENIED

    static readonly HashSet<int> UnavailableDevice =
    [
        unchecked((int)0x88890004), // AUDCLNT_E_DEVICE_INVALIDATED: unplugged, out of range, disabled
        unchecked((int)0x80070490), // E_NOTFOUND: the endpoint went away before it could be opened
        unchecked((int)0x8889000A), // AUDCLNT_E_DEVICE_IN_USE: held in exclusive mode by another app
        unchecked((int)0x8889000F), // AUDCLNT_E_ENDPOINT_CREATE_FAILED
        unchecked((int)0x88890010), // AUDCLNT_E_SERVICE_NOT_RUNNING: the audio service is restarting
        unchecked((int)0x88890026), // AUDCLNT_E_RESOURCES_INVALIDATED
    ];

    /// <summary>Tells what <paramref name="error"/>, or an error it wraps, means for the Source.</summary>
    public static CaptureError Classify(Exception error)
    {
        for (var e = error; e != null; e = e.InnerException)
        {
            if (e.HResult == AccessDenied) return CaptureError.AccessDenied;
            if (UnavailableDevice.Contains(e.HResult)) return CaptureError.Unavailable;
        }
        return CaptureError.Unrecoverable;
    }
}
