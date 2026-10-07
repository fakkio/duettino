using System.Buffers.Binary;
using System.Drawing;

namespace Duettino.Tests;

/// <summary>The icon the window gives Windows for its title bar, the taskbar and Alt+Tab.</summary>
public sealed class MainFormIconTests
{
    [Fact]
    public void The_window_shows_Duettinos_icon() => HiddenMainForm.Run(form =>
    {
        Assert.Equal(File.ReadAllBytes("Duettino.ico"), IcoData(form.Icon!));
    });

    [Fact]
    public void The_window_icon_is_drawn_at_every_size_from_16_to_256() => HiddenMainForm.Run(form =>
    {
        Assert.Equal([16, 20, 24, 32, 40, 48, 64, 256], FrameSizes(IcoData(form.Icon!)));
    });

    /// <remarks>
    /// The sizes the title bar, the taskbar and Alt+Tab ask for between 100% and 250% scaling. 256 px is Explorer's,
    /// read from the executable: System.Drawing never picks that frame for a window.
    /// </remarks>
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(40)]
    [InlineData(48)]
    [InlineData(64)]
    public void The_window_gets_a_frame_of_exactly_the_size_the_display_scaling_asks_for(int size) => HiddenMainForm.Run(form =>
    {
        using var frame = new Icon(form.Icon!, size, size);

        Assert.Equal(new Size(size, size), frame.Size);
    });

    static byte[] IcoData(Icon icon)
    {
        using var data = new MemoryStream();
        icon.Save(data);
        return data.ToArray();
    }

    /// <summary>The width of each frame in .ico data, from its directory: one byte each, 0 meaning 256.</summary>
    static int[] FrameSizes(byte[] ico)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));
        return [.. Enumerable.Range(0, count).Select(i => ico[6 + 16 * i] is 0 ? 256 : ico[6 + 16 * i])];
    }
}
