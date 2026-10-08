using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace Duettino.Tests;

/// <summary>The main window, created and laid out on a UI thread of its own without being shown.</summary>
static class HiddenMainForm
{
    public static void Run(Action<MainForm> test)
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
}
