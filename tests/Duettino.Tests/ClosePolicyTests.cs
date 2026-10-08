namespace Duettino.Tests;

public sealed class ClosePolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closing_with_nothing_running_closes(bool sessionEnding) =>
        Assert.Equal(CloseAction.Close, ClosePolicy.Decide(WindowActivity.Idle, sessionEnding, closeRequested: false));

    [Fact]
    public void Closing_during_a_Recording_asks_whether_to_stop_and_save() =>
        Assert.Equal(CloseAction.AskStopAndSave, ClosePolicy.Decide(WindowActivity.Recording, sessionEnding: false, closeRequested: false));

    [Fact]
    public void Closing_during_Finalization_waits_for_it_and_closing_again_leaves_the_Working_file()
    {
        Assert.Equal(CloseAction.CloseWhenFinalized, ClosePolicy.Decide(WindowActivity.Finalizing, sessionEnding: false, closeRequested: false));
        Assert.Equal(CloseAction.LeaveWorkingFile, ClosePolicy.Decide(WindowActivity.Finalizing, sessionEnding: false, closeRequested: true));
    }

    [Fact]
    public void Windows_shutting_down_or_logging_off_leaves_the_Working_file_without_asking()
    {
        Assert.Equal(CloseAction.LeaveWorkingFile, ClosePolicy.Decide(WindowActivity.Recording, sessionEnding: true, closeRequested: false));
        Assert.Equal(CloseAction.LeaveWorkingFile, ClosePolicy.Decide(WindowActivity.Finalizing, sessionEnding: true, closeRequested: false));
        Assert.Equal(CloseAction.LeaveWorkingFile, ClosePolicy.Decide(WindowActivity.Finalizing, sessionEnding: true, closeRequested: true));
    }
}
