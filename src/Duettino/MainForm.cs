using System.Diagnostics;
using System.Globalization;
using Duettino.Devices;
using Duettino.Engine;
using Duettino.Resources;
using Duettino.Selection;

namespace Duettino;

/// <summary>The single window, laid out in code (ADR-0006).</summary>
sealed class MainForm : Form
{
    // Windows reports one plug or unplug as a burst of notifications: the lists are read again once it is over.
    const int DeviceRefreshDelayMs = 300;

    static readonly Source[] Sources = [Source.Input, Source.Output];

    readonly WindowsDeviceCatalogue catalogue;
    readonly System.Windows.Forms.Timer deviceRefresh = new() { Interval = DeviceRefreshDelayMs };
    readonly Dictionary<Source, ComboBox> lists = Sources.ToDictionary(s => s, _ => DeviceList());
    readonly Dictionary<Source, LevelMeter> meters = Sources.ToDictionary(s => s, _ => Meter());
    readonly System.Windows.Forms.Timer meterRefresh = new() { Interval = LevelMeter.RefreshIntervalMs };
    readonly Label deviceNotice = NoticeLabel();
    readonly Label folderPath = new()
    {
        AutoEllipsis = true, Width = 320, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    readonly Button changeFolderButton = SmallButton(Strings.ChangeFolderButton);
    readonly Button openFolderButton = SmallButton(Strings.OpenFolderButton);
    readonly Button recordButton = new() { AutoSize = true, Padding = new Padding(12, 4, 12, 4), Anchor = AnchorStyles.Left };
    readonly Label elapsed = new()
    {
        Text = FormatElapsed(TimeSpan.Zero), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 3, 3, 3),
    };
    readonly Label notice = NoticeLabel();
    readonly ToolTip toolTip = new();
    Settings settings = Settings.Load(Settings.DefaultPath);
    readonly Dictionary<Source, SourceSelection> selections = [];
    Recorder? recorder;

    public MainForm()
    {
        Text = Strings.AppTitle;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(12);

        var folderButtons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        folderButtons.Controls.AddRange([changeFolderButton, openFolderButton]);
        var recordRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        recordRow.Controls.AddRange([recordButton, elapsed]);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        layout.Controls.Add(FieldLabel(Strings.InputLabel), 0, 0);
        layout.Controls.Add(lists[Source.Input], 1, 0);
        layout.Controls.Add(meters[Source.Input], 1, 1);
        layout.Controls.Add(FieldLabel(Strings.OutputLabel), 0, 2);
        layout.Controls.Add(lists[Source.Output], 1, 2);
        layout.Controls.Add(meters[Source.Output], 1, 3);
        layout.Controls.Add(deviceNotice, 1, 4);
        layout.Controls.Add(FieldLabel(Strings.FolderLabel), 0, 5);
        layout.Controls.Add(folderPath, 1, 5);
        layout.Controls.Add(folderButtons, 1, 6);
        layout.Controls.Add(recordRow, 1, 7);
        layout.Controls.Add(notice, 0, 8);
        layout.SetColumnSpan(notice, 2);
        Controls.Add(layout);

        recordButton.Text = Strings.RecordButton;
        recordButton.Click += OnRecordButtonClick;
        foreach (var (source, list) in lists)
            list.SelectionChangeCommitted += (_, _) => Choose(source, list.SelectedItem as AudioEndpoint);
        changeFolderButton.Click += (_, _) => ChangeFolder();
        openFolderButton.Click += (_, _) => OpenFolder();
        meterRefresh.Tick += (_, _) => ShowLevels();

        catalogue = new WindowsDeviceCatalogue();
        catalogue.Changed += () =>
        {
            deviceRefresh.Stop();
            deviceRefresh.Start();
        };
        deviceRefresh.Tick += (_, _) =>
        {
            deviceRefresh.Stop();
            // The Sources are locked while recording: the lists are read again when it stops.
            if (recorder == null) SelectSources();
        };

        SelectSources();
        ShowFolder();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            catalogue.Dispose();
            deviceRefresh.Dispose();
            meterRefresh.Dispose();
            toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    static ComboBox DeviceList() =>
        new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Anchor = AnchorStyles.Left | AnchorStyles.Right };

    static LevelMeter Meter() =>
        new() { Width = 320, Height = 6, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 0, 3, 6) };

    /// <summary>The timer's text: hours, minutes and whole seconds, the hours going past 24 if need be.</summary>
    internal static string FormatElapsed(TimeSpan time) =>
        string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);

    static Label FieldLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    static Label NoticeLabel() =>
        new() { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 8, 3, 3) };

    static Button SmallButton(string text) =>
        new() { Text = text, AutoSize = true };

    /// <summary>Selects an endpoint for each Source from the active ones, shows it and says when a Source is in Fallback.</summary>
    void SelectSources()
    {
        var fallbacks = new List<string>();
        foreach (var source in Sources)
        {
            var selection = selections[source] = SourceSelection.For(catalogue, source, settings.Chosen(source));
            Fill(lists[source], selection);
            if (selection.IsFallback)
                fallbacks.Add(string.Format(
                    source == Source.Input ? Strings.InputFallback : Strings.OutputFallback,
                    selection.Chosen!.Name,
                    selection.InUse!.Name));
        }
        deviceNotice.Text = string.Join(Environment.NewLine, fallbacks);
        deviceNotice.Visible = fallbacks.Count > 0;
    }

    void Fill(ComboBox list, SourceSelection selection)
    {
        list.BeginUpdate();
        list.Items.Clear();
        if (selection.Endpoints.Count == 0)
        {
            list.Items.Add(Strings.NoDevice);
            list.SelectedIndex = 0;
        }
        else
        {
            list.Items.AddRange([.. selection.Endpoints]);
            list.SelectedItem = selection.InUse;
        }
        list.EndUpdate();
        list.Enabled = recorder == null && selection.Endpoints.Count > 0;
    }

    /// <summary>The user picked <paramref name="endpoint"/> for <paramref name="source"/>: it is remembered from now on.</summary>
    void Choose(Source source, AudioEndpoint? endpoint)
    {
        if (endpoint == null) return;
        settings = settings.WithChosen(source, endpoint);
        SaveSettings();
        SelectSources();
    }

    void ShowFolder()
    {
        folderPath.Text = settings.Folder;
        toolTip.SetToolTip(folderPath, settings.Folder);
    }

    void ChangeFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Strings.ChooseFolder,
            UseDescriptionForTitle = true,
            InitialDirectory = settings.Folder,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings = settings with { Folder = dialog.SelectedPath };
        SaveSettings();
        ShowFolder();
    }

    void OpenFolder()
    {
        try
        {
            // Before the first Recording the default folder doesn't exist yet.
            Directory.CreateDirectory(settings.Folder);
            Process.Start(new ProcessStartInfo(settings.Folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotOpenFolder, settings.Folder, ex.Message);
        }
    }

    // Remembering is a convenience: a settings file that can't be written must not get in the way of recording.
    void SaveSettings()
    {
        try
        {
            settings.Save(Settings.DefaultPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// A Source recorded on its preselected Windows default, with nothing chosen yet, keeps that endpoint from now on.
    /// A chosen endpoint that is absent stays chosen: the Fallback doesn't replace it.
    /// </summary>
    void RememberPreselected()
    {
        var remembered = settings;
        foreach (var source in Sources)
            if (settings.Chosen(source) == null && selections[source].InUse is { } preselected)
                settings = settings.WithChosen(source, preselected);
        if (settings != remembered) SaveSettings();
    }

    async void OnRecordButtonClick(object? sender, EventArgs e)
    {
        if (recorder == null) StartRecording();
        else await StopRecording();
    }

    void StartRecording()
    {
        try
        {
            recorder = Recorder.Start(selections[Source.Input].InUse?.Id, selections[Source.Output].InUse?.Id, settings.Folder);
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotStart, ex.Message);
            return;
        }
        RememberPreselected();
        recorder.SourceFailed += (source, ex) => BeginInvoke(() =>
            notice.Text = string.Format(Strings.SourceFailed, source == Source.Input ? Strings.Input : Strings.Output, ex.Message));
        foreach (var list in lists.Values) list.Enabled = false;
        changeFolderButton.Enabled = false;
        recordButton.Text = Strings.StopButton;
        notice.Text = Strings.RecordingNotice;
        elapsed.Text = FormatElapsed(TimeSpan.Zero);
        meterRefresh.Start();
    }

    /// <summary>
    /// Moves the meters and the timer. They only run while recording: the Input is opened at Record, so Windows shows
    /// the "microphone in use" indicator only then.
    /// </summary>
    void ShowLevels()
    {
        if (recorder == null) return;
        foreach (var (source, meter) in meters) meter.ShowPeak(recorder.TakePeak(source));
        elapsed.Text = FormatElapsed(recorder.Elapsed);
    }

    async Task StopRecording()
    {
        var stopping = recorder!;
        meterRefresh.Stop();
        foreach (var meter in meters.Values) meter.Reset();
        recordButton.Enabled = false;
        recordButton.Text = Strings.RecordButton;
        notice.Text = Strings.SavingNotice;
        try
        {
            await stopping.StopAsync();
            elapsed.Text = FormatElapsed(stopping.Elapsed);
            await FinalizeRecording(stopping.WorkingFilePath);
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotStop, ex.Message);
        }
        recorder = null;
        SelectSources();
        changeFolderButton.Enabled = true;
        recordButton.Enabled = true;
    }

    /// <summary>Turns the Working file into the Recording file off the UI thread, then says how it went.</summary>
    async Task FinalizeRecording(string workingFilePath)
    {
        try
        {
            var result = await Task.Run(() => Finalization.Run(workingFilePath, new MediaFoundationMp3Encoder()));
            notice.Text = string.Format(result.IsWav ? Strings.SavedAsWav : Strings.Saved, result.RecordingFilePath);
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotFinalize, ex.Message, workingFilePath);
        }
    }
}
