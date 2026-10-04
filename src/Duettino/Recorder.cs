using Duettino.Devices;
using Duettino.Engine;

namespace Duettino;

/// <summary>
/// Runs one Recording against real devices: a capture per Source feeding the Recording, and a pacer thread that
/// lets the Recording write its Working file as the clock advances. A Source can switch endpoint mid-Recording
/// (Fallback, return): the Recording places each packet by its timestamp whichever device it came from.
/// </summary>
sealed class Recorder
{
    const int PacerIntervalMs = 10;

    readonly Recording recording;
    readonly Dictionary<Source, SourceCapture?> captures; // guarded by itself
    readonly Thread pacer;
    readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool capturesDisposed;
    volatile bool abandoned;

    Recorder(Recording recording)
    {
        this.recording = recording;
        captures = new() { [Source.Input] = null, [Source.Output] = null };
        pacer = new Thread(Pace) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "Duettino pacer" };
    }

    /// <summary>
    /// Raised, from any thread, when a Source's capture stops or can't be opened (a <see cref="CaptureError.Unavailable"/>
    /// or <see cref="CaptureError.AccessDenied"/>): the Source is recorded as silence until <see cref="Switch"/> opens it again.
    /// </summary>
    public event Action<Source, CaptureError>? SourceLost;

    /// <summary>
    /// Raised, from any thread, when the Recording can't go on: an unrecoverable capture error, or the Working file
    /// can't be written. Stop it: <see cref="StopAsync"/> then completes, with that error if the Working file was closed.
    /// </summary>
    public event Action<Exception>? Failed;

    public string WorkingFilePath => recording.WorkingFilePath;

    /// <inheritdoc cref="Recording.Elapsed"/>
    public TimeSpan Elapsed => recording.Elapsed;

    /// <inheritdoc cref="Recording.TakePeak"/>
    public float TakePeak(Source source) => recording.TakePeak(source);

    /// <summary>
    /// Opens the Working file in <paramref name="folder"/>, the Recording's clock starting now. Subscribe to the events,
    /// then <see cref="Start"/> at once: no Source is captured before.
    /// </summary>
    public static Recorder Create(string folder) =>
        new(Recording.Start(folder, new SystemClock()));

    /// <summary>
    /// Starts recording, each Source from its endpoint (none: silence). A Source that can't be opened is recorded as
    /// silence, and <see cref="SourceLost"/> or <see cref="Failed"/> says why, as for <see cref="Switch"/>.
    /// </summary>
    public void Start(string? inputId, string? outputId)
    {
        pacer.Start();
        Switch(Source.Input, inputId);
        Switch(Source.Output, outputId);
    }

    /// <summary>
    /// Moves <paramref name="source"/> to <paramref name="endpointId"/> (none: silence), closing its current capture.
    /// If the endpoint can't be opened, the Source records silence and <see cref="SourceLost"/> is raised, at once on
    /// this thread; <see cref="Failed"/> instead if the error is <see cref="CaptureError.Unrecoverable"/>.
    /// </summary>
    public void Switch(Source source, string? endpointId)
    {
        SourceCapture? old;
        lock (captures)
        {
            if (capturesDisposed) return;
            old = captures[source];
            captures[source] = null;
        }
        old?.Dispose();
        if (endpointId == null) return;

        SourceCapture? capture = null;
        try
        {
            capture = new SourceCapture(source, endpointId);
            capture.Failed += OnCaptureFailed;
            lock (captures)
            {
                if (capturesDisposed) throw new ObjectDisposedException(nameof(Recorder));
                captures[source] = capture;
            }
            capture.Start(recording);
        }
        catch (ObjectDisposedException)
        {
            capture?.Dispose();
        }
        catch (Exception ex)
        {
            lock (captures)
            {
                if (captures[source] == capture) captures[source] = null;
            }
            capture?.Dispose();
            Report(source, ex);
        }
    }

    /// <summary>
    /// True while <paramref name="source"/> has a capture running: a <see cref="SourceLost"/> that arrives then is
    /// about a capture it has already switched away from.
    /// </summary>
    public bool IsCapturing(Source source)
    {
        lock (captures) return captures[source] is { HasFailed: false };
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
            DisposeCaptures();
        }
    }

    /// <summary>
    /// Closes the Working file at once, its header up to date, without waiting for the audio still in flight: for Windows
    /// shutting down, which won't wait for a Stop. The Working file is left for Recovery; a pending <see cref="StopAsync"/>
    /// never completes.
    /// </summary>
    public void Abandon()
    {
        abandoned = true;
        DisposeCaptures();
        recording.Dispose();
    }

    void DisposeCaptures()
    {
        List<SourceCapture> toDispose;
        lock (captures)
        {
            if (capturesDisposed) return;
            capturesDisposed = true;
            toDispose = [.. captures.Values.OfType<SourceCapture>()];
            foreach (var source in captures.Keys) captures[source] = null;
        }
        foreach (var capture in toDispose) capture.Dispose();
    }

    /// <summary>
    /// A capture stopped with an error: only the Source's current capture counts, not one it switched away from.
    /// It stays in place, delivering nothing, until <see cref="Switch"/> or Stop disposes it: not here, on its own thread.
    /// </summary>
    void OnCaptureFailed(SourceCapture capture, Exception error)
    {
        lock (captures)
        {
            if (captures[capture.Source] != capture) return;
        }
        Report(capture.Source, error);
    }

    void Report(Source source, Exception error)
    {
        var kind = CaptureErrors.Classify(error);
        if (kind == CaptureError.Unrecoverable) Failed?.Invoke(error);
        else SourceLost?.Invoke(source, kind);
    }

    void Pace()
    {
        try
        {
            while (!recording.IsCompleted)
            {
                Thread.Sleep(PacerIntervalMs);
                if (abandoned) return;
                recording.Advance();
            }
            completed.SetResult();
        }
        catch (Exception ex)
        {
            recording.Dispose();
            completed.SetException(ex);
            Failed?.Invoke(ex);
        }
    }
}
