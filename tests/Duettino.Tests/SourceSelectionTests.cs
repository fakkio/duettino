using Duettino.Engine;
using Duettino.Selection;

namespace Duettino.Tests;

public sealed class SourceSelectionTests
{
    static readonly AudioEndpoint Headset = new("{headset-mic}", "Headset Microphone");
    static readonly AudioEndpoint Laptop = new("{laptop-mic}", "Microphone Array");
    static readonly AudioEndpoint Headphones = new("{headphones}", "Headphones");
    static readonly AudioEndpoint Speakers = new("{speakers}", "Speakers");

    [Fact]
    public void A_remembered_Input_that_is_connected_is_used()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Laptop, Headset).WithDefaultInput(Laptop);

        var selection = SourceSelection.For(devices, Source.Input, Headset);

        Assert.Equal([Laptop, Headset], selection.Endpoints);
        Assert.Equal(Headset, selection.InUse);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void A_remembered_Output_that_is_not_connected_falls_back_to_the_Windows_default()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Speakers).WithDefaultOutput(Speakers);

        var selection = SourceSelection.For(devices, Source.Output, Headphones);

        Assert.Equal([Speakers], selection.Endpoints);
        Assert.Equal(Speakers, selection.InUse);
        Assert.True(selection.IsFallback);
    }

    [Fact]
    public void With_nothing_remembered_yet_the_Windows_default_is_preselected_without_a_Fallback()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Headset, Laptop).WithDefaultInput(Laptop);

        var selection = SourceSelection.For(devices, Source.Input, chosen: null);

        Assert.Equal(Laptop, selection.InUse);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void With_no_device_at_all_the_Source_uses_none_and_there_is_no_default_to_fall_back_to()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Speakers).WithDefaultOutput(Speakers);

        var selection = SourceSelection.For(devices, Source.Input, Headset);

        Assert.Empty(selection.Endpoints);
        Assert.Null(selection.InUse);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void Without_a_Windows_default_a_missing_Input_falls_back_to_the_first_connected_one()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Laptop, Headset);

        var selection = SourceSelection.For(devices, Source.Input, new AudioEndpoint("{usb-mic}", "USB Microphone"));

        Assert.Equal(Laptop, selection.InUse);
        Assert.True(selection.IsFallback);
    }
}
