namespace Duettino;

/// <summary>What the window is busy with, as far as closing it goes.</summary>
enum WindowActivity
{
    Idle,
    Recording,

    /// <summary>From Stop until the Recording file is written, or while Recovery finalizes at startup ("Saving…").</summary>
    Finalizing,
}

/// <summary>What closing the window does.</summary>
enum CloseAction
{
    /// <summary>Nothing would be lost: the window closes.</summary>
    Close,

    /// <summary>Ask "Stop and save?": if confirmed, stop, finalize, then close.</summary>
    AskStopAndSave,

    /// <summary>Stay open with "Saving…" and close once Finalization has produced the Recording file.</summary>
    CloseWhenFinalized,

    /// <summary>
    /// Close without asking, leaving the Working file for Recovery: once the window has closed, a running Recording has
    /// its Working file closed with its header up to date, a running Finalization is cancelled.
    /// </summary>
    LeaveWorkingFile,
}

/// <summary>Closing never loses a Recording by accident, and never holds up Windows shutting down or logging off.</summary>
static class ClosePolicy
{
    /// <param name="activity">What the window is busy with.</param>
    /// <param name="sessionEnding">Windows is shutting down or logging off.</param>
    /// <param name="closeRequested">The window was already asked to close once while finalizing.</param>
    public static CloseAction Decide(WindowActivity activity, bool sessionEnding, bool closeRequested) =>
        activity switch
        {
            WindowActivity.Idle => CloseAction.Close,
            _ when sessionEnding => CloseAction.LeaveWorkingFile,
            WindowActivity.Recording => CloseAction.AskStopAndSave,
            _ => closeRequested ? CloseAction.LeaveWorkingFile : CloseAction.CloseWhenFinalized,
        };
}
