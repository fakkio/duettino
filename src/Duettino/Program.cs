using System.Globalization;

namespace Duettino;

static class Program
{
    [STAThread]
    static void Main()
    {
        OverrideUICulture();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    /// <summary>
    /// The UI language follows the Windows display language (CurrentUICulture). DUETTINO_UI_CULTURE (e.g. "en", "it")
    /// overrides it, to check the other language without switching Windows'; an unknown value is ignored.
    /// </summary>
    static void OverrideUICulture()
    {
        if (Environment.GetEnvironmentVariable("DUETTINO_UI_CULTURE") is not { Length: > 0 } name) return;
        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
        }
    }
}
