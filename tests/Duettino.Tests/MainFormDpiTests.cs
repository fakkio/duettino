using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Duettino.Tests;

/// <summary>
/// The window moved to a screen with another DPI, as Windows tells it (WM_DPICHANGED), without showing it: a real
/// move needs two screens, and showing it would offer Recovery for the user's own files.
/// </summary>
public sealed class MainFormDpiTests
{
    // The width of the device lists, the meters and the folder path at 96 DPI (MainForm.FieldWidth).
    const int FieldWidthAt96Dpi = 320;

    /// <remarks>
    /// The window is created on the primary screen, so this only fails on the bug (sizes left at 96 DPI) when that screen
    /// is above 100% scaling: a test can't start the window at another DPI. At 100% it passes trivially.
    /// </remarks>
    [Fact]
    public void Started_on_a_screen_above_100_percent_the_fields_are_scaled_to_it() => HiddenMainForm.Run(form =>
    {
        var expected = FieldWidthAt96Dpi * form.DeviceDpi / 96.0;
        var fields = Descendants(form).Where(c => c is ComboBox or LevelMeter).ToList();

        Assert.NotEmpty(fields);
        foreach (var field in fields)
            Assert.InRange(field.Width, expected * 0.9, expected * 1.1);
    });

    [Fact]
    public void Moving_to_a_screen_with_another_DPI_and_back_leaves_the_window_as_it_was() => HiddenMainForm.Run(form =>
    {
        var home = form.DeviceDpi;
        var away = AnotherDpi(home);
        var before = Layout(form);

        for (var trip = 0; trip < 3; trip++)
        {
            ChangeDpi(form, away);
            ChangeDpi(form, home);
        }

        Assert.Equal(before, Layout(form));
    });

    [Fact]
    public void On_a_screen_with_another_DPI_the_fields_grow_with_it_and_nothing_overlaps() => HiddenMainForm.Run(form =>
    {
        var fields = Descendants(form).Where(c => c is ComboBox or LevelMeter).ToList();
        var before = fields.ToDictionary(c => c, c => c.Size);
        var firstLabel = Descendants(form).OfType<Label>().First();
        int Inset() => form.PointToClient(firstLabel.Parent!.PointToScreen(firstLabel.Location)).X;
        var inset = Inset();
        var away = AnotherDpi(form.DeviceDpi);
        var ratio = away / (double)form.DeviceDpi;

        ChangeDpi(form, away);

        Assert.InRange(Inset(), inset * ratio * 0.9, inset * ratio * 1.1);
        foreach (var field in fields)
        {
            Assert.InRange(field.Width, before[field].Width * ratio * 0.9, before[field].Width * ratio * 1.1);
            // A list's height follows its font, which has a floor at low DPI: going down it only has to shrink.
            var tallest = ratio < 1 ? before[field].Height - 1 : before[field].Height * ratio * 1.1;
            Assert.InRange(field.Height, before[field].Height * ratio * 0.75, tallest);
        }
        var shown = Descendants(form).Where(c => c.Visible && !c.Bounds.Size.IsEmpty).ToList();
        foreach (var a in shown)
            foreach (var b in shown.Where(b => b != a && b.Parent == a.Parent))
                Assert.False(a.Bounds.IntersectsWith(b.Bounds), $"{a.GetType().Name} {a.Bounds} overlaps {b.GetType().Name} {b.Bounds}");
    });

    /// <summary>A DPI to move to that differs a lot from <paramref name="home"/>: 250% up from a low one, 100% down from a high one.</summary>
    static int AnotherDpi(int home) => home >= 192 ? 96 : home * 5 / 2;

    /// <summary>What Windows sends when the window moves to a screen at <paramref name="dpi"/>: the window, resized in proportion.</summary>
    static void ChangeDpi(Form form, int dpi)
    {
        const int WM_DPICHANGED = 0x02E0;
        var ratio = dpi / (double)form.DeviceDpi;
        var suggested = new Rect(form.Left, form.Top, form.Left + (int)(form.Width * ratio), form.Top + (int)(form.Height * ratio));
        SendMessage(form.Handle, WM_DPICHANGED, (dpi << 16) | dpi, ref suggested);
        Application.DoEvents();
    }

    static string Layout(Form form)
    {
        var text = new StringBuilder($"window {form.ClientSize}\n");
        foreach (var control in Descendants(form)) text.Append($"{control.GetType().Name} {control.Bounds}\n");
        return text.ToString();
    }

    static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => Descendants(c).Prepend(c));

    [StructLayout(LayoutKind.Sequential)]
    record struct Rect(int Left, int Top, int Right, int Bottom);

    [DllImport("user32.dll")]
    static extern nint SendMessage(nint hWnd, int msg, nint wParam, ref Rect lParam);
}
