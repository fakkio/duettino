using System.Runtime.InteropServices;
using Duettino.Devices;
using Duettino.Engine;
using Duettino.Selection;

namespace Duettino.Tests;

public sealed class AudioSystemTests
{
    static readonly AudioEndpoint Headset = new("headset", "Headset");

    // As on Windows Server Core, which has no Windows Audio service.
    static COMException ServiceMissing() =>
        new("The specified service does not exist as an installed service.", unchecked((int)0x80070424));

    [Fact]
    public void A_catalogue_that_opens_is_used_as_it_is()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Headset).WithDefaultInput(Headset);

        var audio = AudioSystem.Open(() => devices);

        Assert.Same(devices, audio.Catalogue);
        Assert.Null(audio.Unavailable);
    }

    [Fact]
    public void Windows_audio_that_cant_be_reached_leaves_no_device_and_says_why()
    {
        var error = ServiceMissing();

        var audio = AudioSystem.Open(() => throw error);

        Assert.Same(error, audio.Unavailable);
        AssertEmpty(audio.Catalogue);
    }

    [Fact]
    public void A_catalogue_that_opens_but_cant_be_read_is_closed_and_left_out()
    {
        var unreadable = new UnreadableCatalogue();

        var audio = AudioSystem.Open(() => unreadable);

        Assert.IsType<COMException>(audio.Unavailable);
        Assert.True(unreadable.Disposed);
        AssertEmpty(audio.Catalogue);
    }

    [Fact]
    public void Errors_other_than_Windows_audio_are_not_hidden() =>
        Assert.Throws<InvalidOperationException>(() => AudioSystem.Open(() => throw new InvalidOperationException()));

    static void AssertEmpty(IDeviceCatalogue catalogue)
    {
        foreach (var source in new[] { Source.Input, Source.Output })
        {
            Assert.Empty(catalogue.Active(source));
            Assert.Null(catalogue.DefaultId(source));
            Assert.Null(SourceSelection.For(catalogue, source, Headset).InUse);
        }
    }

    sealed class UnreadableCatalogue : IDeviceCatalogue, IDisposable
    {
        public bool Disposed { get; private set; }

        public event Action? Changed { add { } remove { } }

        public IReadOnlyList<AudioEndpoint> Active(Source source) => throw ServiceMissing();

        public string? DefaultId(Source source) => throw ServiceMissing();

        public void Dispose() => Disposed = true;
    }
}
