using System.Runtime.ExceptionServices;
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
    [Fact]
    public void Moving_to_a_screen_with_another_DPI_and_back_leaves_the_window_as_it_was() => OnUIThread(form =>
    {
        var home = form.DeviceDpi;
        var before = Layout(form);

        for (var trip = 0; trip < 3; trip++)
        {
            ChangeDpi(form, home * 5 / 2);
            ChangeDpi(form, home);
        }

        Assert.Equal(before, Layout(form));
    });

    [Fact]
    public void On_a_screen_with_another_DPI_the_fields_grow_with_it_and_nothing_overlaps() => OnUIThread(form =>
    {
        var fields = Descendants(form).Where(c => c is ComboBox or LevelMeter).ToList();
        var before = fields.ToDictionary(c => c, c => c.Size);
        var firstLabel = Descendants(form).OfType<Label>().First();
        int Inset() => form.PointToClient(firstLabel.Parent!.PointToScreen(firstLabel.Location)).X;
        var inset = Inset();

        ChangeDpi(form, form.DeviceDpi * 5 / 2);

        Assert.InRange(Inset(), inset * 2.5 * 0.9, inset * 2.5 * 1.1);
        foreach (var field in fields)
        {
            Assert.InRange(field.Width, before[field].Width * 2.5 * 0.9, before[field].Width * 2.5 * 1.1);
            Assert.InRange(field.Height, before[field].Height * 2.5 * 0.75, before[field].Height * 2.5 * 1.1);
        }
        var shown = Descendants(form).Where(c => c.Visible && !c.Bounds.Size.IsEmpty).ToList();
        foreach (var a in shown)
            foreach (var b in shown.Where(b => b != a && b.Parent == a.Parent))
                Assert.False(a.Bounds.IntersectsWith(b.Bounds), $"{a.GetType().Name} {a.Bounds} overlaps {b.GetType().Name} {b.Bounds}");
    });

    static void OnUIThread(Action<MainForm> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // as the app's manifest asks
                using var form = new MainForm();
                _ = form.Handle;
                form.PerformLayout();
                test(form);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Throw(failure);
    }

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
