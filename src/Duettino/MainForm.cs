using Duettino.Devices;
using Duettino.Engine;
using Duettino.Resources;

namespace Duettino;

/// <summary>The single window, laid out in code (ADR-0006).</summary>
sealed class MainForm : Form
{
    static readonly string RecordingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Duettino");

    readonly ComboBox inputList = DeviceList();
    readonly ComboBox outputList = DeviceList();
    readonly Button recordButton = new() { AutoSize = true, Padding = new Padding(12, 4, 12, 4), Anchor = AnchorStyles.Left };
    readonly Label notice = new() { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 8, 3, 3) };
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

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        layout.Controls.Add(FieldLabel(Strings.InputLabel), 0, 0);
        layout.Controls.Add(inputList, 1, 0);
        layout.Controls.Add(FieldLabel(Strings.OutputLabel), 0, 1);
        layout.Controls.Add(outputList, 1, 1);
        layout.Controls.Add(recordButton, 1, 2);
        layout.Controls.Add(notice, 0, 3);
        layout.SetColumnSpan(notice, 2);
        Controls.Add(layout);

        recordButton.Text = Strings.RecordButton;
        recordButton.Click += OnRecordButtonClick;

        Fill(inputList, AudioDevices.Inputs(), AudioDevices.DefaultInputId());
        Fill(outputList, AudioDevices.Outputs(), AudioDevices.DefaultOutputId());
    }

    static ComboBox DeviceList() =>
        new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Anchor = AnchorStyles.Left | AnchorStyles.Right };

    static Label FieldLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    static void Fill(ComboBox list, IReadOnlyList<AudioEndpoint> endpoints, string? defaultId)
    {
        list.Items.Clear();
        if (endpoints.Count == 0)
        {
            list.Items.Add(Strings.NoDevice);
            list.SelectedIndex = 0;
            list.Enabled = false;
            return;
        }
        list.Items.AddRange([.. endpoints]);
        list.SelectedItem = endpoints.FirstOrDefault(e => e.Id == defaultId) ?? endpoints[0];
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
            recorder = Recorder.Start(
                (inputList.SelectedItem as AudioEndpoint)?.Id,
                (outputList.SelectedItem as AudioEndpoint)?.Id,
                RecordingsFolder);
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotStart, ex.Message);
            return;
        }
        recorder.SourceFailed += (source, ex) => BeginInvoke(() =>
            notice.Text = string.Format(Strings.SourceFailed, source == Source.Input ? Strings.Input : Strings.Output, ex.Message));
        inputList.Enabled = outputList.Enabled = false;
        recordButton.Text = Strings.StopButton;
        notice.Text = Strings.RecordingNotice;
    }

    async Task StopRecording()
    {
        var stopping = recorder!;
        recordButton.Enabled = false;
        try
        {
            await stopping.StopAsync();
            notice.Text = string.Format(Strings.WorkingFileSaved, stopping.WorkingFilePath);
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotStop, ex.Message);
        }
        recorder = null;
        inputList.Enabled = inputList.Items[0] is AudioEndpoint;
        outputList.Enabled = outputList.Items[0] is AudioEndpoint;
        recordButton.Text = Strings.RecordButton;
        recordButton.Enabled = true;
    }
}
