using System.Runtime.InteropServices;
using Duettino.Devices;
using NAudio.CoreAudioApi;

namespace Duettino.Tests;

public sealed class CaptureErrorsTests
{
    [Theory]
    [InlineData(unchecked((int)0x88890004))] // AUDCLNT_E_DEVICE_INVALIDATED: unplugged, as the spike saw
    [InlineData(unchecked((int)0x80070490))] // E_NOTFOUND: the endpoint is gone before it is opened
    [InlineData(unchecked((int)0x8889000A))] // AUDCLNT_E_DEVICE_IN_USE: held in exclusive mode by another app
    [InlineData(unchecked((int)0x88890026))] // AUDCLNT_E_RESOURCES_INVALIDATED
    public void A_device_that_went_away_or_is_busy_is_unavailable(int hresult) =>
        Assert.Equal(CaptureError.Unavailable, CaptureErrors.Classify(new CoreAudioException(hresult)));

    [Fact]
    public void Access_denied_by_Windows_privacy_settings_is_told_apart_however_it_is_wrapped()
    {
        Assert.Equal(CaptureError.AccessDenied, CaptureErrors.Classify(new UnauthorizedAccessException()));
        Assert.Equal(CaptureError.AccessDenied, CaptureErrors.Classify(new COMException("denied", unchecked((int)0x80070005))));
        Assert.Equal(
            CaptureError.AccessDenied,
            CaptureErrors.Classify(new InvalidOperationException("wrapped", new UnauthorizedAccessException())));
    }

    [Fact]
    public void Anything_else_cant_be_recovered_from()
    {
        Assert.Equal(CaptureError.Unrecoverable, CaptureErrors.Classify(new InvalidOperationException()));
        Assert.Equal(CaptureError.Unrecoverable, CaptureErrors.Classify(new CoreAudioException(unchecked((int)0x88890001))));
    }
}
