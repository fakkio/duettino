namespace Duettino.Tests;

public sealed class ElapsedTimeTests
{
    [Theory]
    [InlineData(0, "0:00:00")]
    [InlineData(59.9, "0:00:59")]
    [InlineData(3723, "1:02:03")]
    [InlineData(90000, "25:00:00")]
    public void The_timer_shows_hours_minutes_and_whole_seconds(double seconds, string expected) =>
        Assert.Equal(expected, MainForm.FormatElapsed(TimeSpan.FromSeconds(seconds)));
}
