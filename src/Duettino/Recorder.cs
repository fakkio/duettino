using Duettino.Devices;
using Duettino.Engine;

namespace Duettino;

/// <summary>
/// Runs one Recording against real devices: a capture per Source feeding the Recording, and a pacer thread that
/// lets the Recording write its Working file as the clock advances.
/// </summary>
sealed class Recorder
{
    const int PacerIntervalMs = 10;

    readonly Recording recording;
    readonly List<SourceCapture> captures;
    readonly Thread pacer;
    readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    Recorder(Recording recording, List<SourceCapture> captures)
    {
        this.recording = recording;
        this.captures = captures;
        pacer = new Thread(Pace) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "Duettino pacer" };
    }

    /// <summary>Raised from a capture thread when a Source stops delivering audio; it is recorded as silence from then on.</summary>
    public event Action<Source, Exception>? SourceFailed;

    public string WorkingFilePath => recording.WorkingFilePath;

    /// <summary>
    /// Opens a capture on each chosen endpoint and starts recording into <paramref name="folder"/>.
    /// A Source with no endpoint is recorded as silence.
    /// </summary>
    public static Recorder Start(string? inputId, string? outputId, string folder)
    {
        var captures = new List<SourceCapture>();
        Recording? recording = null;
        try
        {
            if (inputId != null) captures.Add(new SourceCapture(Source.Input, inputId));
            if (outputId != null) captures.Add(new SourceCapture(Source.Output, outputId));
            recording = Recording.Start(folder, new SystemClock());
            var recorder = new Recorder(recording, captures);
            foreach (var capture in captures)
            {
                capture.Failed += (source, ex) => recorder.SourceFailed?.Invoke(source, ex);
                capture.Start(recording);
            }
            recorder.pacer.Start();
            return recorder;
        }
        catch
        {
            foreach (var capture in captures) capture.Dispose();
            if (recording != null)
            {
                // Nothing worth keeping was recorded yet: don't leave an empty Working file behind.
                recording.Dispose();
                File.Delete(recording.WorkingFilePath);
            }
            throw;
        }
    }

    /// <summary>Stops the Recording and completes once the Working file is written and closed.</summary>
    public async Task StopAsync()
    {
        recording.Stop();
        try
        {
            await completed.Task;
        }
        finally
        {
            foreach (var capture in captures) capture.Dispose();
        }
    }

    void Pace()
    {
        try
        {
            while (!recording.IsCompleted)
            {
                Thread.Sleep(PacerIntervalMs);
                recording.Advance();
            }
            completed.SetResult();
        }
        catch (Exception ex)
        {
            recording.Dispose();
            completed.SetException(ex);
        }
    }
}
