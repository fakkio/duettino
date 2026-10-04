using System.Diagnostics;
using System.Globalization;
using Duettino.Devices;
using Duettino.Engine;
using Duettino.Resources;
using Duettino.Selection;
using Microsoft.VisualBasic.FileIO;

namespace Duettino;

/// <summary>The single window, laid out in code (ADR-0006).</summary>
sealed class MainForm : Form
{
    // Windows reports one plug or unplug as a burst of notifications: the lists are read again once it is over.
    const int DeviceRefreshDelayMs = 300;

    // A cancelled Finalization stops at its next read, well within this, and deletes its partial file before exiting.
    const int FinalizationCancelWaitMs = 1000;

    static readonly Source[] Sources = [Source.Input, Source.Output];

    readonly WindowsDeviceCatalogue catalogue;
    readonly System.Windows.Forms.Timer deviceRefresh = new() { Interval = DeviceRefreshDelayMs };
    readonly Dictionary<Source, ComboBox> lists = Sources.ToDictionary(s => s, _ => DeviceList());
    readonly Dictionary<Source, LevelMeter> meters = Sources.ToDictionary(s => s, _ => Meter());
    readonly System.Windows.Forms.Timer recordingTick = new() { Interval = LevelMeter.RefreshIntervalMs };
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
    readonly Dictionary<Source, SourceFollower> followers = []; // while recording
    readonly SystemClock clock = new();
    Recorder? recorder;
    bool finalizing;
    CancellationTokenSource? finalizationCancellation;
    Task? finalization; // the running Finalization, off the UI thread
    bool closeWhenFinalized;
    bool sessionEnding; // handling WM_QUERYENDSESSION
    bool leaving; // the window is closing and leaving the Working file: nothing more is shown
    bool microphoneDenied; // the Input is denied by the Windows privacy settings, and tried again until allowed

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
        recordingTick.Tick += (_, _) =>
        {
            ShowLevels();
            FollowDevices();
        };

        catalogue = new WindowsDeviceCatalogue();
        catalogue.Changed += () =>
        {
            deviceRefresh.Stop();
            deviceRefresh.Start();
        };
        deviceRefresh.Tick += (_, _) =>
        {
            deviceRefresh.Stop();
            // While recording, each Source's follower reads the catalogue and switches its capture (FollowDevices).
            if (recorder == null) SelectSources();
        };

        SelectSources();
        ShowFolder();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await OfferRecovery();
    }

    /// <remarks>
    /// Windows shutting down or logging off is told by WM_QUERYENDSESSION itself, not by the FormClosing event's
    /// CloseReason: WinForms leaves that at WindowsShutDown after a shutdown another app cancels, so a later close from
    /// elsewhere (Task Manager's End task) would skip "Stop and save?".
    /// </remarks>
    protected override void WndProc(ref Message m)
    {
        const int WM_QUERYENDSESSION = 0x0011;
        var outer = sessionEnding;
        sessionEnding = m.Msg == WM_QUERYENDSESSION;
        try
        {
            base.WndProc(ref m);
        }
        finally
        {
            sessionEnding = outer;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        switch (ClosePolicy.Decide(Activity, sessionEnding, closeWhenFinalized))
        {
            case CloseAction.AskStopAndSave:
                e.Cancel = true;
                // While the question was open, Windows may have shut the Recording down, or an error stopped it already.
                if (!ConfirmStopAndSave() || leaving) break;
                if (Activity != WindowActivity.Idle) closeWhenFinalized = true;
                if (Activity == WindowActivity.Recording) _ = StopRecording();
                break;
            case CloseAction.CloseWhenFinalized:
                e.Cancel = true;
                closeWhenFinalized = true;
                break;
        }
    }

    /// <remarks>
    /// <see cref="CloseAction.LeaveWorkingFile"/> lets the window close and acts only here, once it really does:
    /// a Windows shutdown or logoff closes it only when it goes ahead (WM_ENDSESSION), so one that another app cancels
    /// leaves the Recording running.
    /// </remarks>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (Activity != WindowActivity.Idle) LeaveWorkingFile();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            catalogue.Dispose();
            deviceRefresh.Dispose();
            recordingTick.Dispose();
            toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    WindowActivity Activity =>
        finalizing ? WindowActivity.Finalizing : recorder != null ? WindowActivity.Recording : WindowActivity.Idle;

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

    /// <summary>Selects an endpoint for each Source from the active ones, and shows it.</summary>
    void SelectSources()
    {
        foreach (var source in Sources) selections[source] = SourceSelection.For(catalogue, source, settings.Chosen(source));
        ShowSelections();
    }

    /// <summary>
    /// Shows the endpoint each Source uses, and says when one is in Fallback, has no device left, or has lost its
    /// capture and waits to open it again.
    /// </summary>
    void ShowSelections()
    {
        var notices = new List<string>();
        foreach (var source in Sources)
        {
            var selection = selections[source];
            Fill(lists[source], selection);
            var input = source == Source.Input;
            // Denied access is said by its own notice, not as a device that stopped working.
            if (followers.TryGetValue(source, out var follower) && follower.IsLost && !(input && microphoneDenied))
                notices.Add(string.Format(input ? Strings.InputLost : Strings.OutputLost, selection.InUse!.Name));
            else if (selection.IsFallback)
                notices.Add(string.Format(
                    input ? Strings.InputFallback : Strings.OutputFallback, selection.Chosen!.Name, selection.InUse!.Name));
            else if (selection is { Chosen: { } chosen, InUse: null })
                notices.Add(string.Format(input ? Strings.InputMissing : Strings.OutputMissing, chosen.Name));
        }
        deviceNotice.Text = string.Join(Environment.NewLine, notices);
        deviceNotice.Visible = notices.Count > 0;
    }

    /// <summary>
    /// An error as the notices show it: in the UI language when the user can act on it, Windows' or .NET's own message
    /// otherwise.
    /// </summary>
    static string ErrorText(Exception error)
    {
        const int DiskFull = unchecked((int)0x80070070), HandleDiskFull = unchecked((int)0x80070027);
        return error.HResult is DiskFull or HandleDiskFull ? Strings.DiskFull : error.Message;
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
        Recorder started;
        try
        {
            started = Recorder.Create(settings.Folder);
        }
        catch (Exception ex)
        {
            notice.Text = string.Format(Strings.CannotStart, ErrorText(ex));
            return;
        }
        recorder = started;
        microphoneDenied = false;
        // Each Source follows its devices from now on, the preselected Windows default counting as chosen.
        foreach (var source in Sources)
            followers[source] = new SourceFollower(catalogue, clock, source, settings.Chosen(source) ?? selections[source].InUse);
        RememberPreselected();
        started.SourceLost += (source, error) => BeginInvoke(() => OnSourceLost(started, source, error));
        started.Failed += error => BeginInvoke(() => OnRecordingFailed(started, error));
        changeFolderButton.Enabled = false;
        recordButton.Text = Strings.StopButton;
        notice.Text = Strings.RecordingNotice;
        elapsed.Text = FormatElapsed(TimeSpan.Zero);
        started.Start(InUseId(Source.Input), InUseId(Source.Output));
        foreach (var (source, follower) in followers) selections[source] = follower.Selection;
        ShowSelections();
        recordingTick.Start();
    }

    string? InUseId(Source source) => followers[source].Selection.InUse?.Id;

    void StopFollowing()
    {
        foreach (var follower in followers.Values) follower.Dispose();
        followers.Clear();
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

    /// <summary>Moves each Source to the endpoint its follower says, as devices come and go, and shows it.</summary>
    void FollowDevices()
    {
        if (recorder == null) return;
        var changed = false;
        foreach (var (source, follower) in followers)
        {
            if (follower.Poll()) recorder.Switch(source, follower.Selection.InUse?.Id);
            if (follower.Selection == selections[source]) continue;
            selections[source] = follower.Selection;
            changed = true;
        }
        if (microphoneDenied && recorder.IsCapturing(Source.Input))
        {
            // Allowed again in the privacy settings: the Input is being recorded.
            microphoneDenied = false;
            notice.Text = Strings.RecordingNotice;
            changed = true;
        }
        if (changed) ShowSelections();
    }

    /// <summary>
    /// A Source's capture stopped or couldn't be opened: it records silence until its follower finds it an endpoint
    /// again. Access to the microphone denied in the Windows privacy settings is said, and tried again like a lost
    /// device, so the Input starts as soon as it is allowed. A loss that arrives after the Source has already switched
    /// to a working capture is about the old one, and is ignored.
    /// </summary>
    void OnSourceLost(Recorder lostBy, Source source, CaptureError error)
    {
        if (recorder != lostBy || Activity != WindowActivity.Recording || lostBy.IsCapturing(source)) return;
        if (error == CaptureError.AccessDenied && source == Source.Input)
        {
            microphoneDenied = true;
            notice.Text = Strings.MicrophoneDenied;
        }
        followers[source].Lost();
        ShowSelections();
    }

    /// <summary>The Recording can't go on: it is stopped and finalized as on Stop, the notice saying why.</summary>
    void OnRecordingFailed(Recorder failed, Exception error)
    {
        if (recorder != failed || Activity != WindowActivity.Recording || leaving) return;
        _ = StopRecording(error);
    }

    /// <summary>
    /// Stops the Recording and finalizes it. One stopped by <paramref name="error"/>, or failing to stop, is finalized
    /// all the same as far as its Working file goes, and the notice says what went wrong.
    /// </summary>
    async Task StopRecording(Exception? error = null)
    {
        var stopping = recorder!;
        recordingTick.Stop();
        StopFollowing();
        foreach (var meter in meters.Values) meter.Reset();
        recordButton.Text = Strings.RecordButton;
        BeginFinalizing();
        try
        {
            await stopping.StopAsync();
        }
        catch (Exception ex)
        {
            error ??= ex;
        }
        elapsed.Text = FormatElapsed(stopping.Elapsed);
        var (text, finalized) = await FinalizeRecording(stopping.WorkingFilePath);
        if (leaving) return;
        notice.Text = error == null ? text : string.Format(Strings.CannotStop, ErrorText(error)) + Environment.NewLine + text;
        recorder = null;
        SelectSources();
        EndFinalizing(finalized);
    }

    /// <summary>
    /// Turns the Working file into the Recording file off the UI thread; returns the notice saying how it went and
    /// whether it produced the Recording file.
    /// </summary>
    async Task<(string Notice, bool Finalized)> FinalizeRecording(string workingFilePath)
    {
        using var cancellation = new CancellationTokenSource();
        var running = Task.Run(() => Finalization.Run(workingFilePath, new MediaFoundationMp3Encoder(), cancellation.Token));
        (finalizationCancellation, finalization) = (cancellation, running);
        try
        {
            var result = await running;
            return (string.Format(result.IsWav ? Strings.SavedAsWav : Strings.Saved, result.RecordingFilePath), true);
        }
        catch (Exception ex)
        {
            return (string.Format(Strings.CannotFinalize, ErrorText(ex), workingFilePath), false);
        }
        finally
        {
            (finalizationCancellation, finalization) = (null, null);
        }
    }

    /// <summary>Locks Record and "Change…" and shows "Saving…", from Stop or Recovery until <see cref="EndFinalizing"/>.</summary>
    void BeginFinalizing()
    {
        finalizing = true;
        recordButton.Enabled = false;
        changeFolderButton.Enabled = false;
        notice.Text = Strings.SavingNotice;
    }

    /// <summary>
    /// Unlocks the window. If it was asked to close meanwhile, it closes now, unless Finalization failed: then it stays
    /// open, so the notice saying so is seen.
    /// </summary>
    void EndFinalizing(bool finalized)
    {
        finalizing = false;
        changeFolderButton.Enabled = true;
        recordButton.Enabled = true;
        var close = closeWhenFinalized && finalized;
        closeWhenFinalized = false;
        if (close) Close();
    }

    bool ConfirmStopAndSave()
    {
        var stopAndSave = new TaskDialogButton(Strings.StopAndSaveButton);
        var page = new TaskDialogPage
        {
            Caption = Strings.AppTitle,
            Heading = Strings.StopAndSaveHeading,
            Text = Strings.StopAndSaveText,
            Icon = TaskDialogIcon.Warning,
            Buttons = { stopAndSave, new TaskDialogButton(Strings.KeepRecordingButton) },
            DefaultButton = stopAndSave,
            AllowCancel = true,
        };
        return TaskDialog.ShowDialog(this, page) == stopAndSave;
    }

    /// <summary>
    /// The window closed while busy: the Working file is left for Recovery. A running Recording closes it with its
    /// header up to date; a running Finalization is cancelled and given a moment to delete its partial file.
    /// </summary>
    void LeaveWorkingFile()
    {
        leaving = true;
        recordingTick.Stop();
        recorder?.Abandon();
        finalizationCancellation?.Cancel();
        try
        {
            finalization?.Wait(FinalizationCancelWaitMs);
        }
        catch (AggregateException)
        {
            // Cancelled, as asked, or failed: the Working file stays either way.
        }
    }

    /// <summary>
    /// Asks what to do with each Orphan Working file in the destination folder (Recover, Delete or Later), then
    /// finalizes the ones to recover, one after the other, with Record locked as during any Finalization.
    /// One with no audio at all holds nothing to recover: it is deleted without asking. So are the partial files of
    /// Finalizations killed midway whose Working file is gone.
    /// </summary>
    async Task OfferRecovery()
    {
        IReadOnlyList<OrphanWorkingFile> orphans;
        try
        {
            orphans = Recovery.FindOrphans(settings.Folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            notice.Text = string.Format(Strings.CannotFindOrphans, settings.Folder, ex.Message);
            return;
        }

        var toRecover = new List<string>();
        var notices = new List<string>();
        foreach (var orphan in orphans)
        {
            if (orphan.Length == TimeSpan.Zero)
            {
                DeleteEmpty(orphan.Path);
                continue;
            }
            switch (AskAboutOrphan(orphan))
            {
                case RecoveryChoice.Recover:
                    toRecover.Add(orphan.Path);
                    break;
                case RecoveryChoice.Delete:
                    if (MoveToRecycleBin(orphan.Path) is { } error) notices.Add(error);
                    break;
            }
        }
        Recovery.DeleteStalePartialFiles(settings.Folder);
        if (toRecover.Count == 0)
        {
            notice.Text = string.Join(Environment.NewLine, notices);
            return;
        }
        BeginFinalizing();
        var finalized = true;
        foreach (var path in toRecover)
        {
            var (text, recovered) = await FinalizeRecording(path);
            if (leaving) return;
            notices.Add(text);
            finalized &= recovered;
            // Asked to close: the window closes once this one is done, and the others are offered at the next launch.
            if (closeWhenFinalized) break;
        }
        notice.Text = string.Join(Environment.NewLine, notices);
        EndFinalizing(finalized);
    }

    enum RecoveryChoice { Recover, Delete, Later }

    RecoveryChoice AskAboutOrphan(OrphanWorkingFile orphan)
    {
        var recover = new TaskDialogCommandLinkButton(Strings.RecoverButton, Strings.RecoverDescription);
        var delete = new TaskDialogCommandLinkButton(Strings.DeleteButton, Strings.DeleteDescription);
        var later = new TaskDialogCommandLinkButton(Strings.LaterButton, Strings.LaterDescription);
        var length = orphan.Length is { } known ? FormatElapsed(known) : Strings.RecoveryLengthUnknown;
        var page = new TaskDialogPage
        {
            Caption = Strings.AppTitle,
            Heading = Strings.RecoveryHeading,
            Text = string.Format(Strings.RecoveryText, Path.GetFileName(orphan.Path), length),
            Icon = TaskDialogIcon.Warning,
            Buttons = { recover, delete, later },
            DefaultButton = recover,
            // Closing the dialog decides nothing: the file is offered again at the next launch.
            AllowCancel = true,
        };
        var clicked = TaskDialog.ShowDialog(this, page);
        return clicked == recover ? RecoveryChoice.Recover : clicked == delete ? RecoveryChoice.Delete : RecoveryChoice.Later;
    }

    static void DeleteEmpty(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only a few bytes: it is tried again at the next launch.
        }
    }

    /// <summary>Deletes the file into the Recycle Bin, so a wrong click can be undone; returns the notice if it couldn't.</summary>
    static string? MoveToRecycleBin(string path)
    {
        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return null;
        }
        catch (Exception ex)
        {
            return string.Format(Strings.CannotDelete, path, ex.Message);
        }
    }
}
