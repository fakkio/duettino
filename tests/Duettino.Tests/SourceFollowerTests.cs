using Duettino.Engine;
using Duettino.Selection;

namespace Duettino.Tests;

public sealed class SourceFollowerTests
{
    static readonly AudioEndpoint Headset = new("{headset-mic}", "Headset Microphone");
    static readonly AudioEndpoint Laptop = new("{laptop-mic}", "Microphone Array");
    static readonly AudioEndpoint Headphones = new("{headphones}", "Headphones");
    static readonly AudioEndpoint Speakers = new("{speakers}", "Speakers");
    static readonly AudioEndpoint Hdmi = new("{hdmi}", "HDMI");

    readonly FakeClock clock = new();

    [Fact]
    public void An_Output_whose_device_is_unplugged_falls_back_to_the_Windows_default_once_the_burst_of_events_is_over()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        using var follower = new SourceFollower(devices, clock, Source.Output, Headphones);
        Assert.Equal(Headphones, follower.Selection.InUse);

        // Windows reports the unplug, then moves the default, a moment apart.
        devices.WithOutputs(Speakers);
        clock.AdvanceMs(100);
        devices.WithDefaultOutput(Speakers);
        clock.AdvanceMs(100);
        Assert.False(follower.Poll());

        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);
        Assert.True(follower.Poll());
        Assert.Equal(Speakers, follower.Selection.InUse);
        Assert.True(follower.Selection.IsFallback);
        Assert.False(follower.Poll());
    }

    [Fact]
    public void The_chosen_Output_coming_back_is_used_again_as_soon_as_Windows_sends_the_sound_there()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Speakers).WithDefaultOutput(Speakers);
        using var follower = new SourceFollower(devices, clock, Source.Output, Headphones);
        Assert.True(follower.Selection.IsFallback);

        // Windows makes the headphones the default as they come back: the sound is there now, not on the speakers.
        devices.WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);

        Assert.True(follower.Poll());
        Assert.Equal(Headphones, follower.Selection.InUse);
        Assert.False(follower.Selection.IsFallback);
    }

    [Fact]
    public void The_chosen_Output_coming_back_while_the_sound_stays_elsewhere_is_used_again_once_devices_have_been_quiet_for_a_while()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Speakers).WithDefaultOutput(Speakers);
        using var follower = new SourceFollower(devices, clock, Source.Output, Headphones);

        devices.WithOutputs(Headphones, Speakers);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);
        // The Fallback still carries the sound: no hurry to leave it while the headphones may still be settling.
        Assert.False(follower.Poll());
        Assert.Equal(Speakers, follower.Selection.InUse);

        clock.AdvanceMs((SourceFollower.StableTime - SourceFollower.SettleTime).TotalMilliseconds);
        Assert.True(follower.Poll());
        Assert.Equal(Headphones, follower.Selection.InUse);
    }

    [Fact]
    public void During_a_Fallback_the_Output_follows_the_Windows_default_as_soon_as_it_moves()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Speakers, Hdmi).WithDefaultOutput(Speakers);
        using var follower = new SourceFollower(devices, clock, Source.Output, Headphones);

        devices.WithDefaultOutput(Hdmi);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);

        Assert.True(follower.Poll());
        Assert.Equal(Hdmi, follower.Selection.InUse);
        Assert.True(follower.Selection.IsFallback);
    }

    [Fact]
    public void The_chosen_Input_stays_in_use_when_the_Windows_default_moves_elsewhere()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Headset, Laptop).WithDefaultInput(Headset);
        using var follower = new SourceFollower(devices, clock, Source.Input, Headset);

        devices.WithDefaultInput(Laptop);
        clock.AdvanceMs(SourceFollower.StableTime.TotalMilliseconds);

        Assert.False(follower.Poll());
        Assert.Equal(Headset, follower.Selection.InUse);
    }

    [Fact]
    public void With_no_device_left_the_Input_uses_none_and_takes_the_first_one_that_comes()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Headset).WithDefaultInput(Headset);
        using var follower = new SourceFollower(devices, clock, Source.Input, Headset);

        devices.WithInputs().WithDefaultInput(null);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);
        Assert.True(follower.Poll());
        Assert.Null(follower.Selection.InUse);

        devices.WithInputs(Laptop).WithDefaultInput(Laptop);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);
        // Nothing is being recorded: no reason to wait any longer.
        Assert.True(follower.Poll());
        Assert.Equal(Laptop, follower.Selection.InUse);
        Assert.True(follower.Selection.IsFallback);
    }

    [Fact]
    public void Losing_the_Output_doesnt_move_the_Input()
    {
        var devices = new FakeDeviceCatalogue()
            .WithInputs(Headset, Laptop).WithDefaultInput(Headset)
            .WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        using var input = new SourceFollower(devices, clock, Source.Input, Headset);
        using var output = new SourceFollower(devices, clock, Source.Output, Headphones);

        devices.WithOutputs(Speakers).WithDefaultOutput(Speakers);
        clock.AdvanceMs(SourceFollower.StableTime.TotalMilliseconds);

        Assert.True(output.Poll());
        Assert.False(input.Poll());
        Assert.Equal(Headset, input.Selection.InUse);
    }

    [Fact]
    public void The_Output_falls_back_without_waiting_for_the_Input_devices_to_settle()
    {
        var devices = new FakeDeviceCatalogue()
            .WithInputs(Headset, Laptop).WithDefaultInput(Headset)
            .WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        using var output = new SourceFollower(devices, clock, Source.Output, Headphones);

        // A headset pulled out: the headphones go first, its microphone a moment later, as the loopback spike saw it.
        devices.WithOutputs(Speakers).WithDefaultOutput(Speakers);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds - 100);
        devices.WithInputs(Laptop).WithDefaultInput(Laptop);
        clock.AdvanceMs(100);

        Assert.True(output.Poll());
        Assert.Equal(Speakers, output.Selection.InUse);
    }

    [Fact]
    public void Bluetooth_headphones_flapping_while_they_reconnect_switch_the_Output_once_away_and_once_back()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        using var follower = new SourceFollower(devices, clock, Source.Output, Headphones);
        var switches = new List<AudioEndpoint?>();

        // Into the case: gone, the default moves to the speakers.
        devices.WithOutputs(Speakers).WithDefaultOutput(Speakers);
        PollFor(follower, 1000, switches);
        // Reconnecting, as the loopback spike saw it: unplugged, not present, unplugged… a little longer apart than a
        // burst, and active for a moment now and then, before Windows sends the sound there.
        for (int flap = 0; flap < 4; flap++)
        {
            devices.WithOutputs(Speakers);
            PollFor(follower, 700, switches);
            devices.WithOutputs(Headphones, Speakers);
            PollFor(follower, 700, switches);
            devices.WithOutputs(Speakers);
            PollFor(follower, 700, switches);
        }
        // Back for good.
        devices.WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        PollFor(follower, 5000, switches);

        Assert.Equal([Speakers, Headphones], switches);
    }

    [Fact]
    public void A_capture_lost_on_a_device_Windows_still_lists_is_opened_again_after_a_pause_not_at_once()
    {
        var devices = new FakeDeviceCatalogue().WithInputs(Headset, Laptop).WithDefaultInput(Headset);
        using var follower = new SourceFollower(devices, clock, Source.Input, Headset);

        follower.Lost();
        Assert.True(follower.IsLost);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);
        Assert.False(follower.Poll());
        Assert.True(follower.IsLost);

        clock.AdvanceMs((SourceFollower.RetryTime - SourceFollower.SettleTime).TotalMilliseconds);
        Assert.True(follower.Poll());
        Assert.Equal(Headset, follower.Selection.InUse);
        Assert.False(follower.IsLost);
        Assert.False(follower.Poll());
    }

    [Fact]
    public void A_capture_lost_as_its_device_goes_falls_back_as_soon_as_Windows_has_reported_it()
    {
        var devices = new FakeDeviceCatalogue().WithOutputs(Headphones, Speakers).WithDefaultOutput(Headphones);
        using var follower = new SourceFollower(devices, clock, Source.Output, Headphones);

        follower.Lost();
        clock.AdvanceMs(100);
        devices.WithOutputs(Speakers).WithDefaultOutput(Speakers);
        clock.AdvanceMs(SourceFollower.SettleTime.TotalMilliseconds);

        Assert.True(follower.Poll());
        Assert.Equal(Speakers, follower.Selection.InUse);
    }

    /// <summary>Polls <paramref name="follower"/> every 100 ms for <paramref name="ms"/>, as the window does, noting each switch.</summary>
    void PollFor(SourceFollower follower, int ms, List<AudioEndpoint?> switches)
    {
        for (int elapsed = 0; elapsed < ms; elapsed += 100)
        {
            clock.AdvanceMs(100);
            if (follower.Poll()) switches.Add(follower.Selection.InUse);
        }
    }
}
