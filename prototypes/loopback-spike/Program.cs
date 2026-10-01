// PROTOTYPE, throwaway: hardware spike for Duettino, lives only on branch prototype/loopback-spike, never merged.
// Question: do WASAPI Input capture + Loopback capture behave on real devices the way ADR-0002..0005 assume?

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.MediaFoundation;
using NAudio.Wave;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var opt = Options.Parse(args);
if (opt.Analyze != null)
{
    Log.Open(Path.Combine(opt.Analyze, $"echo-{opt.WinS}s.txt"));
    Log.W($"Offline echo analysis of {opt.Analyze}, {opt.WinS} s windows");
    Post.Echo(Path.Combine(opt.Analyze, "working.wav"), opt.WinS);
    Log.Close();
    return;
}
var enumerator = new MMDeviceEnumerator();

var inputs = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
var outputs = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
Log.W($"Duettino loopback spike (PROTOTYPE), {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {RuntimeInformation.FrameworkDescription}, {Environment.OSVersion}");
Devices.List("Capture devices (candidates for Input)", inputs, DataFlow.Capture, enumerator);
Devices.List("Playback devices (candidates for Output)", outputs, DataFlow.Render, enumerator);
if (opt.ListOnly) return;

var inDev = inputs[(opt.In ?? Ask.Int("Input number", 1, 1, inputs.Count)) - 1];
var outDev = outputs[(opt.Out ?? Ask.Int("Output number", 1, 1, outputs.Count)) - 1];
int seconds = opt.Seconds ?? Ask.Int("Seconds to record", 30, 1, 36000);
string label = opt.Label ?? Ask.Str("Label for this run (e.g. wired, bt, speakers, silence)", "run");
bool mp3 = opt.Mp3 ?? Ask.YesNo("Also write an MP3 of the Mix via Media Foundation", false);

Eng.Stamp = opt.Fill != "naive";
Eng.LatencyMs = opt.LatencyMs;
Eng.TolMs = opt.TolMs;
Eng.GapMs = opt.GapMs;
Eng.BufferMs = opt.BufferMs;
Eng.InputMode = opt.InputMode;

string dir = Path.Combine(Paths.ProjectDir(), "runs", $"{DateTime.Now:yyyyMMdd-HHmmss}-{label}");
Directory.CreateDirectory(dir);
Log.Open(Path.Combine(dir, "report.txt"));
Log.W("");
Log.W($"Run '{label}': {seconds} s, Input = {inDev.FriendlyName}, Output = {outDev.FriendlyName}");
Log.W($"Engine: fill={(Eng.Stamp ? "stamp (each packet placed at its capture QPC timestamp)" : "naive (silence only on engine underrun)")}, latency {Eng.LatencyMs} ms, tolerance {Eng.TolMs} ms, gap threshold {Eng.GapMs} ms, capture buffer {Eng.BufferMs} ms, Input stream mode {Eng.InputMode}");
Log.W($"Input volume {inDev.AudioEndpointVolume.MasterVolumeLevelScalar:P0}, Output volume {outDev.AudioEndpointVolume.MasterVolumeLevelScalar:P0} mute {outDev.AudioEndpointVolume.Mute}");
Log.W($"Folder: {dir}");

var inp = new Source("Input", loopback: false, channels: 1, inDev.ID);
var outp = new Source("Output", loopback: true, channels: 2, outDev.ID);

void OnData(Source s, ReadOnlySpan<byte> b, AudioClientBufferFlags flags, long devPos, long qpc)
{
    try
    {
        double now = Eng.NowMs, cap = Eng.QpcToMs(qpc);
        if (b.Length == 0) return;
        var f = s.Format;
        int frames = b.Length / f.BlockAlign;
        bool silentFlag = flags.HasFlag(AudioClientBufferFlags.Silent);
        bool zero = silentFlag || b.IndexOfAnyExcept((byte)0) < 0;
        var mapped = silentFlag ? new float[frames * s.Ch] : Fmt.Map(Fmt.ToFloat(b, f, frames), frames, f.Channels, s.Ch);
        float pk = 0; double ss = 0;
        foreach (var v in mapped) { var a = Math.Abs(v); if (a > pk) pk = a; ss += v * v; }
        var (o, n48) = s.Resample(mapped, frames);
        lock (s)
        {
            if (silentFlag) s.Raw?.Write(new byte[b.Length]); else s.Raw?.Write(b);
            s.Packets++; s.WinPackets++;
            if (zero) s.ZeroPackets++;
            if (silentFlag) s.SilentFlags++;
            if (flags.HasFlag(AudioClientBufferFlags.DataDiscontinuity)) s.Discontinuities++;
            if (flags.HasFlag(AudioClientBufferFlags.TimestampError)) s.TimestampErrors++;
            s.NativeFrames += frames;
            if (s.LastPacketMs >= 0)
            {
                double dt = now - s.LastPacketMs;
                s.Intervals.Add(dt);
                if (dt > Eng.GapMs) s.Gaps.Add((s.LastPacketMs / 1000, dt));
            }
            else s.FirstPacketMs = now;
            s.LastPacketMs = now;
            s.Delivery.Add(now - (cap + frames * 1000.0 / f.SampleRate));
            // The device position may count at the device rate, not the stream rate (16 kHz in Bluetooth Hands-Free):
            // learn that rate from position against QPC between two packets.
            if (s.NextDevPos >= 0 && cap > s.LastCapMs)
            {
                double r = (devPos - s.LastDevPos) / ((cap - s.LastCapMs) / 1000);
                int std = new[] { 8000, 16000, 24000, 32000, 44100, 48000, 96000 }.OrderBy(q => Math.Abs(q - r)).First();
                if (std != s.PosRate && Math.Abs(r - std) < 0.03 * std)
                {
                    Log.W($"** [{now / 1000,7:F1}s] {s.Name} device position now counts at {std} Hz (stream {f.SampleRate} Hz)");
                    s.PosRate = std; s.EndDriftRun();
                    s.NextDevPos = devPos;
                }
            }
            if (s.NextDevPos >= 0 && devPos != s.NextDevPos)
            {
                // Position jumped (e.g. no data while nothing played): log how far it disagrees with QPC, and restart the drift run
                // so a one-off step is not read as a slope.
                double stepMs = (devPos - s.LastDevPos) * 1000.0 / s.PosRate - (cap - s.LastCapMs);
                Log.W($"** [{now / 1000,7:F1}s] {s.Name} device position jumped {(devPos - s.NextDevPos) * 1000.0 / s.PosRate:F1} ms; position minus QPC step {stepMs:+0.0;-0.0} ms");
                s.PosJumps++; s.PosJumpFrames += devPos - s.NextDevPos;
                if (Math.Abs(stepMs) > 20) s.TimelineBreaks++;
                s.EndDriftRun();
            }
            else if (s.NextDevPos >= 0 && Math.Abs(devPos - (s.LastDevPos + (cap - s.LastCapMs) * s.PosRate / 1000)) > 0.02 * s.PosRate)
            {
                // Timeline break: the position did not move in step with QPC (e.g. it froze while nothing played).
                Log.W($"** [{now / 1000,7:F1}s] {s.Name} timeline break: position advanced {(devPos - s.LastDevPos) * 1000.0 / s.PosRate:F1} ms while QPC advanced {cap - s.LastCapMs:F1} ms");
                s.TimelineBreaks++;
                s.EndDriftRun();
            }
            s.LastDevPos = devPos; s.LastCapMs = cap; s.NextDevPos = devPos + (long)Math.Round(frames * (double)s.PosRate / f.SampleRate);
            s.Run.Add(cap / 1000, devPos);
            if (pk > s.Peak) s.Peak = pk;
            if (pk > s.WinPeak) s.WinPeak = pk;
            s.SumSq += ss; s.N += mapped.Length; s.WinSumSq += ss; s.WinN += mapped.Length;
            s.Push(o, n48, (long)(cap / 1000 * Eng.Rate));
        }
    }
    catch (Exception ex) { Log.W($"!! {s.Name} handler error: {ex}"); }
}

void OnStopped(Source s, StoppedEventArgs e)
{
    if (Eng.Stopping) return;
    var x = e.Exception;
    Log.W($"!! [{Eng.NowMs / 1000,7:F1}s] {s.Name} capture stopped: {(x == null ? "no exception" : $"{x.GetType().Name} 0x{x.HResult:X8} {x.Message}")}");
    Task.Run(() =>
    {
        for (int i = 1; i <= 30 && !Eng.Stopping; i++)
        {
            Thread.Sleep(500);
            try
            {
                s.Open(enumerator, dir, OnData, OnStopped);
                s.Capture.StartRecording();
                Log.W($"!! [{Eng.NowMs / 1000,7:F1}s] {s.Name} reopened (attempt {i}), format {Fmt.Describe(s.Format)}");
                return;
            }
            catch (Exception ex) { Log.W($"!! {s.Name} reopen attempt {i} failed: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}"); }
        }
    });
}

using var notifications = enumerator.CreateNotificationClient(false);
string Who(string id) => id == inp.DeviceId ? "Input" : id == outp.DeviceId ? "Output" : null;
notifications.DeviceStateChanged += (_, e) => { if (Who(e.DeviceId) is { } w) Log.W($"** [{Eng.NowMs / 1000,7:F1}s] {w} state -> {e.NewState}"); };
notifications.DeviceRemoved += (_, e) => { if (Who(e.DeviceId) is { } w) Log.W($"** [{Eng.NowMs / 1000,7:F1}s] {w} removed"); };
notifications.DefaultDeviceChanged += (_, e) => Log.W($"** [{Eng.NowMs / 1000,7:F1}s] default {e.Flow}/{e.Role} changed -> {Who(e.DeviceId) ?? "other device"}");
notifications.PropertyValueChanged += (_, e) => { if (Who(e.DeviceId) is { } w) Log.W($"** [{Eng.NowMs / 1000,7:F1}s] {w} property changed {Devices.KeyName(e.PropertyKey)}"); };

inp.Open(enumerator, dir, OnData, OnStopped);
outp.Open(enumerator, dir, OnData, OnStopped);
Log.W($"Input  capture format: {Fmt.Describe(inp.Format)}");
Log.W($"Output capture format: {Fmt.Describe(outp.Format)}");

string workingPath = Path.Combine(dir, "working.wav");
var working = new WaveFileWriter(workingPath, new WaveFormat(Eng.Rate, 16, 3));
long written = 0;
var inBuf = new float[Eng.Rate]; var outBuf = new float[Eng.Rate * 2]; var pcm = new short[Eng.Rate * 3];
void Pump(long target)
{
    while (written < target)
    {
        int n = (int)Math.Min(target - written, Eng.Rate / 2);
        inp.Take(n, inBuf); outp.Take(n, outBuf);
        for (int i = 0; i < n; i++)
        {
            pcm[3 * i] = Fmt.S16(inBuf[i]);
            pcm[3 * i + 1] = Fmt.S16(outBuf[2 * i]);
            pcm[3 * i + 2] = Fmt.S16(outBuf[2 * i + 1]);
        }
        working.WriteSamples(pcm, 0, n * 3);
        written += n;
    }
}
bool engineStop = false;
var engine = new Thread(() =>
{
    double lastFlush = 0;
    while (!Volatile.Read(ref engineStop))
    {
        Thread.Sleep(5);
        double now = Eng.NowMs;
        Pump((long)((Math.Min(now - Eng.LatencyMs, Eng.StopAtMs)) / 1000 * Eng.Rate));
        if (now - lastFlush >= 1000) { working.Flush(); lastFlush = now; }
    }
}) { IsBackground = true, Priority = ThreadPriority.AboveNormal };

bool quit = false;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit = true; };
Log.W("");
Log.W("Recording. Keys: M = mark an event (e.g. 'volume changed now'), Q = stop early.");
Eng.Start();
inp.Capture.StartRecording();
outp.Capture.StartRecording();
engine.Start();

double nextPrint = 1000; int marks = 0;
while (!quit && Eng.NowMs < seconds * 1000.0)
{
    Thread.Sleep(20);
    while (!Console.IsInputRedirected && Console.KeyAvailable)
    {
        var k = Console.ReadKey(true).Key;
        if (k == ConsoleKey.Q || k == ConsoleKey.Escape) quit = true;
        if (k == ConsoleKey.M) Log.W($">> [{Eng.NowMs / 1000,7:F1}s] MARK {++marks}");
    }
    if (Eng.NowMs >= nextPrint)
    {
        var ev = outDev.AudioEndpointVolume;
        Log.W($"[{Eng.NowMs / 1000,7:F1}s] {inp.Status()}");
        Log.W($"           {outp.Status()} | vol {ev.MasterVolumeLevelScalar:P0}{(ev.Mute ? " MUTED" : "")}");
        nextPrint += 1000;
    }
}

double stopAt = Eng.NowMs;
Eng.StopAtMs = stopAt;
Eng.Stopping = true;
foreach (var s in new[] { inp, outp }) try { s.Capture.StopRecording(); } catch (Exception ex) { Log.W($"!! stop {s.Name}: {ex.Message}"); }
Thread.Sleep(500);
Volatile.Write(ref engineStop, true);
engine.Join();
Pump((long)(stopAt / 1000 * Eng.Rate));
working.Dispose();
foreach (var s in new[] { inp, outp }) lock (s) { s.Raw?.Dispose(); s.Raw = null; s.Capture.Dispose(); }

Log.W("");
Log.W($"===== SUMMARY '{label}': clock {stopAt / 1000:F2} s, Working file {written / (double)Eng.Rate:F2} s ({new FileInfo(workingPath).Length / 1048576.0:F1} MB) =====");
inp.Summary(stopAt); outp.Summary(stopAt);
if (inp.Best is { } bi && outp.Best is { } bo)
    Log.W($"Relative drift Input vs Output (longest continuous runs): {Fmt.Sg(bi.Ppm(inp.BestRate) - bo.Ppm(outp.BestRate))} ppm = {Fmt.Sg((bi.Ppm(inp.BestRate) - bo.Ppm(outp.BestRate)) * 3600 / 1000, "0")} ms per hour");

string mixPath = Path.Combine(dir, "mix.wav");
Post.Mix(workingPath, mixPath);
if (!opt.NoEcho) Post.Echo(workingPath, opt.WinS);
if (mp3) Post.Mp3(mixPath, Path.Combine(dir, "mix.mp3"));
Log.W("");
Log.W($"Done. Files in {dir}");
Log.Close();

static class Eng
{
    public const int Rate = 48000;
    static long startTs = -1;
    public static void Start() => startTs = Stopwatch.GetTimestamp();
    public static double NowMs => startTs < 0 ? 0 : (Stopwatch.GetTimestamp() - startTs) * 1000.0 / Stopwatch.Frequency;
    // WASAPI QPC positions are in 100 ns units.
    public static double QpcToMs(long qpc) => qpc / 1e4 - startTs * 1000.0 / Stopwatch.Frequency;
    public static bool Stamp;
    public static volatile bool Stopping;
    public static double StopAtMs = double.MaxValue;
    public static int LatencyMs, TolMs, GapMs, BufferMs;
    public static string InputMode = "default";
}

sealed class Source(string name, bool loopback, int channels, string deviceId)
{
    public readonly string Name = name;
    public readonly bool Loopback = loopback;
    public readonly int Ch = channels;
    public readonly string DeviceId = deviceId;
    public WasapiRecorder Capture;
    public WaveFormat Format;
    public WaveFileWriter Raw;
    public int NominalRate => Format.SampleRate;
    int segment;
    WdlResampler resampler;
    readonly List<string> formats = [];
    readonly List<(string File, int Rate, Drift Run)> segments = [];

    // Ring buffer of 48 kHz frames; its tail sits at engine position Consumed + Count.
    const int CapFrames = Eng.Rate * 2;
    readonly float[] ring = new float[CapFrames * channels];
    int head;
    public int Count;
    long consumed;
    bool started;
    public long OffsetFrames;

    public long Packets, ZeroPackets, SilentFlags, Discontinuities, TimestampErrors, NativeFrames, WinPackets;
    public long NextDevPos = -1, LastDevPos, PosJumps, PosJumpFrames, TimelineBreaks;
    public int PosRate;
    public double LastCapMs;
    public readonly List<double> Delivery = [];
    public double FirstPacketMs = -1, LastPacketMs = -1;
    public readonly List<double> Intervals = [];
    public readonly List<(double At, double Ms)> Gaps = [];
    long padFrames, dropFrames, underrunFrames, overflowFrames;
    int padEvents, dropEvents;
    public int Restarts = -1;
    public float Peak, WinPeak;
    public double SumSq, WinSumSq;
    public long N, WinN;
    public Drift Run = new(), Best;
    public int BestRate;

    public void Open(MMDeviceEnumerator en, string dir, Action<Source, ReadOnlySpan<byte>, AudioClientBufferFlags, long, long> onData, Action<Source, StoppedEventArgs> onStopped)
    {
        try { Capture?.Dispose(); } catch { }
        var dev = en.GetDevice(DeviceId);
        var builder = new WasapiRecorderBuilder().WithDevice(dev).WithBufferLength(Eng.BufferMs);
        if (Loopback) builder = builder.WithLoopbackCapture();
        else if (Eng.InputMode == "raw") builder = builder.WithRawMode();
        else if (Eng.InputMode == "comms") builder = builder.WithCommunicationsMode();
        Capture = builder.Build();
        Capture.DataAvailable += (b, fl, dp, q) => onData(this, b, fl, dp, q);
        Capture.RecordingStopped += (_, e) => onStopped(this, e);
        lock (this)
        {
            Restarts++;
            var desc = Fmt.Describe(Capture.WaveFormat);
            if (formats.Count > 0 && formats[^1] != desc) Log.W($"!! {Name} FORMAT CHANGE: {formats[^1]}  ->  {desc}");
            formats.Add(desc);
            Format = Capture.WaveFormat;
            Raw?.Dispose();
            segment++;
            var file = Path.Combine(dir, $"{Name.ToLowerInvariant()}.raw{(segment > 1 ? $".{segment}" : "")}.wav");
            Raw = new WaveFileWriter(file, Format);
            EndDriftRun();
            segments.Add((Path.GetFileName(file), Format.SampleRate, null));
            resampler = null;
            if (Format.SampleRate != Eng.Rate)
            {
                resampler = new WdlResampler();
                resampler.SetMode(true, 2, false);
                resampler.SetFilterParms();
                resampler.SetFeedMode(true);
                resampler.SetRates(Format.SampleRate, Eng.Rate);
            }
            LastPacketMs = -1; NextDevPos = -1; PosRate = Format.SampleRate;
        }
    }

    public (float[] Data, int Frames) Resample(float[] x, int frames)
    {
        if (resampler == null) return (x, frames);
        int need = resampler.ResamplePrepare(frames, Ch, out Span<float> inbuf);
        x.AsSpan(0, Math.Min(need, frames) * Ch).CopyTo(inbuf);
        int max = (int)((long)frames * Eng.Rate / Format.SampleRate) + 64;
        var o = new float[max * Ch];
        int got = resampler.ResampleOut(o, Math.Min(need, frames), max, Ch);
        return (o, got);
    }

    public void EndDriftRun()
    {
        if (Run.Duration > (Best?.Duration ?? 0)) { Best = Run; BestRate = PosRate; }
        Run = new Drift();
    }

    // Called under lock(this) with a packet whose last frame arrived at engine position clockPos.
    public void Push(float[] x, int frames, long clockPos)
    {
        int start = 0;
        if (Eng.Stamp)
        {
            long diff = clockPos - frames - (consumed + Count);
            long tol = (long)Eng.TolMs * Eng.Rate / 1000;
            if (diff > (started ? tol : 0))
            {
                Enqueue(null, 0, (int)Math.Min(diff, CapFrames));
                if (started) { padFrames += diff; padEvents++; }
            }
            else if (diff < -(started ? tol : 0))
            {
                start = (int)Math.Min(frames, -diff);
                if (started) { dropFrames += start; dropEvents++; }
            }
        }
        started = true;
        Enqueue(x, start, frames - start);
        OffsetFrames = consumed + Count - clockPos;
    }

    void Enqueue(float[] x, int from, int n)
    {
        int over = Count + n - CapFrames;
        if (over > 0) { head = (head + over) % CapFrames; Count -= over; overflowFrames += over; }
        for (int i = 0; i < n; i++)
        {
            int idx = (head + Count + i) % CapFrames * Ch;
            for (int c = 0; c < Ch; c++) ring[idx + c] = x == null ? 0 : x[(from + i) * Ch + c];
        }
        Count += n;
    }

    public void Take(int n, float[] dst)
    {
        lock (this)
        {
            int got = Math.Min(n, Count);
            for (int i = 0; i < got; i++)
            {
                int idx = (head + i) % CapFrames * Ch;
                for (int c = 0; c < Ch; c++) dst[i * Ch + c] = ring[idx + c];
            }
            Array.Clear(dst, got * Ch, (n - got) * Ch);
            head = (head + got) % CapFrames; Count -= got;
            if (started) underrunFrames += n - got;
            consumed += n;
        }
    }

    static string Ms(long frames) => $"{frames * 1000.0 / Eng.Rate:F0}";

    public string Status()
    {
        lock (this)
        {
            double now = Eng.NowMs;
            string lvl = WinN == 0 ? "pk   -- rms   --" : $"pk {Fmt.Db(WinPeak),5:F1} rms {Fmt.Db(Math.Sqrt(WinSumSq / WinN)),5:F1}";
            string line = $"{Name,-6} {lvl} dBFS | pkts {Packets,6} (+{WinPackets,3}) zero {ZeroPackets,4} silentflag {SilentFlags} | gaps {Gaps.Count} ({Gaps.Sum(g => g.Ms):F0} ms)";
            if (LastPacketMs >= 0 && now - LastPacketMs > Eng.GapMs) line += $" NO DATA for {now - LastPacketMs:F0} ms";
            line += $" | buf {Ms(Count)} ms off {Ms(OffsetFrames)} ms under {Ms(underrunFrames)} ms";
            if (Eng.Stamp) line += $" pad {padEvents}/{Ms(padFrames)} ms drop {dropEvents}/{Ms(dropFrames)} ms";
            if (overflowFrames > 0) line += $" overflow {Ms(overflowFrames)} ms";
            if (PosJumps > 0 || Discontinuities > 0 || TimelineBreaks > 0) line += $" | pos jumps {PosJumps} ({PosJumpFrames * 1000.0 / PosRate:F0} ms) disc {Discontinuities} breaks {TimelineBreaks}";
            if (Run.Duration > 20) line += $" | drift {Fmt.Sg(Run.Ppm(PosRate), "0")} ppm ({Fmt.Sg(Run.RawDiffMs(PosRate))} ms in {Run.Duration:F0} s)";
            WinPackets = 0; WinPeak = 0; WinSumSq = 0; WinN = 0;
            return line;
        }
    }

    public void Summary(double stopMs)
    {
        lock (this)
        {
            EndDriftRun();
            var iv = Intervals.OrderBy(v => v).ToArray();
            double Pct(double p) => iv.Length == 0 ? double.NaN : iv[Math.Min(iv.Length - 1, (int)(p * iv.Length))];
            Log.W($"--- {Name} ({(Loopback ? "Loopback capture" : "capture")}) ---");
            Log.W($"  formats: {string.Join("  |  ", formats.Distinct())}; reopenings {Restarts}; raw files {string.Join(", ", segments.Select(s => s.File))}");
            Log.W($"  recorder: buffer {Eng.BufferMs} ms requested, LatencyMilliseconds {Capture.LatencyMilliseconds}");
            Log.W($"  packets {Packets} (all-zero {ZeroPackets}, SILENT flag {SilentFlags}, DATA_DISCONTINUITY {Discontinuities}, TIMESTAMP_ERROR {TimestampErrors}), first after {FirstPacketMs:F0} ms, native frames {NativeFrames} ({NativeFrames / (double)NominalRate:F2} s at {NominalRate} Hz)");
            Log.W($"  device position: jumps {PosJumps} totalling {PosJumpFrames * 1000.0 / PosRate:F0} ms, timeline breaks (position not following QPC) {TimelineBreaks}");
            var dl = Delivery.OrderBy(v => v).ToArray();
            if (dl.Length > 0) Log.W($"  delivery delay (arrival minus capture QPC of last frame) ms: median {dl[dl.Length / 2]:F1}, p99 {dl[Math.Min(dl.Length - 1, (int)(0.99 * dl.Length))]:F1}, max {dl[^1]:F1}");
            Log.W($"  packet interval ms: median {Pct(0.5):F1}, p99 {Pct(0.99):F1}, max {(iv.Length > 0 ? iv[^1] : double.NaN):F1}");
            Log.W($"  gaps > {Eng.GapMs} ms: {Gaps.Count}, total {Gaps.Sum(g => g.Ms):F0} ms, longest {(Gaps.Count > 0 ? Gaps.Max(g => g.Ms) : 0):F0} ms");
            foreach (var g in Gaps.OrderByDescending(g => g.Ms).Take(8).OrderBy(g => g.At)) Log.W($"      at {g.At,7:F1} s: {g.Ms:F0} ms without data");
            Log.W($"  level: peak {Fmt.Db(Peak):F1} dBFS, RMS {Fmt.Db(Math.Sqrt(SumSq / Math.Max(1, N))):F1} dBFS");
            Log.W($"  engine: underrun (silence filled by the clock) {Ms(underrunFrames)} ms, overflow dropped {Ms(overflowFrames)} ms" +
                  (Eng.Stamp ? $", gap pads {padEvents} ({Ms(padFrames)} ms), drift drops {dropEvents} ({Ms(dropFrames)} ms)" : "") + $", final offset {Ms(OffsetFrames)} ms");
            if (Best != null && Best.Duration > 1)
                Log.W($"  drift of device position vs QPC clock (longest unbroken run, {Best.Duration:F1} s): {Fmt.Sg(Best.Ppm(BestRate))} ppm, device minus clock {Fmt.Sg(Best.RawDiffMs(BestRate))} ms");
        }
    }
}

// Least-squares slope of cumulative native frames against clock time over one run of packets without gaps.
sealed class Drift
{
    double t0 = -1, y0, n, st, sy, stt, sty, lastT, lastY;
    public void Add(double t, double y)
    {
        if (t0 < 0) { t0 = t; y0 = y; }
        double x = t - t0, v = y - y0;
        n++; st += x; sy += v; stt += x * x; sty += x * v; lastT = t; lastY = y;
    }
    public double Duration => n < 2 ? 0 : lastT - t0;
    public double Ppm(int rate) => ((n * sty - st * sy) / (n * stt - st * st) / rate - 1) * 1e6;
    public double RawDiffMs(int rate) => ((lastY - y0) / rate - (lastT - t0)) * 1000;
}

static class Fmt
{
    public static bool IsFloat(WaveFormat f) =>
        f.Encoding == WaveFormatEncoding.IeeeFloat ||
        (f is WaveFormatExtensible x ? x.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT : f.Encoding == WaveFormatEncoding.Extensible && f.BitsPerSample == 32);

    public static string Describe(WaveFormat f)
    {
        string enc = f.Encoding.ToString();
        if (f is WaveFormatExtensible x)
            enc = $"Extensible({(x.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT ? "float" : x.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM ? "PCM" : x.SubFormat.ToString())}, valid {x.ValidBitsPerSample} bit, mask 0x{x.ChannelMask:X})";
        else if (f.Encoding == WaveFormatEncoding.Extensible) enc = $"Extensible(not parsed by NAudio: {f.GetType().Name})";
        return $"{f.SampleRate} Hz, {f.Channels} ch, {f.BitsPerSample} bit, {enc}";
    }

    public static float[] ToFloat(ReadOnlySpan<byte> b, WaveFormat f, int frames)
    {
        int n = frames * f.Channels;
        var o = new float[n];
        if (IsFloat(f)) { MemoryMarshal.Cast<byte, float>(b)[..n].CopyTo(o); return o; }
        switch (f.BitsPerSample)
        {
            case 16: { var s = MemoryMarshal.Cast<byte, short>(b); for (int i = 0; i < n; i++) o[i] = s[i] / 32768f; break; }
            case 24: for (int i = 0; i < n; i++) o[i] = ((b[3 * i] << 8 | b[3 * i + 1] << 16 | b[3 * i + 2] << 24) >> 8) / 8388608f; break;
            case 32: { var s = MemoryMarshal.Cast<byte, int>(b); for (int i = 0; i < n; i++) o[i] = s[i] / 2147483648f; break; }
            default: throw new NotSupportedException(Describe(f));
        }
        return o;
    }

    // Input: average of all channels to mono. Output: first two channels as stereo (mono duplicated).
    public static float[] Map(float[] x, int frames, int inCh, int outCh)
    {
        if (inCh == outCh) return x;
        var o = new float[frames * outCh];
        for (int i = 0; i < frames; i++)
        {
            if (outCh == 1) { float a = 0; for (int c = 0; c < inCh; c++) a += x[i * inCh + c]; o[i] = a / inCh; }
            else { o[2 * i] = x[i * inCh]; o[2 * i + 1] = x[i * inCh + (inCh > 1 ? 1 : 0)]; }
        }
        return o;
    }

    public static short S16(float v) => (short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767f);
    public static string Sg(double v, string f = "0.0") => (Math.Round(v, f.Length > 1 ? f.Length - 2 : 0) + 0.0) is var r && r >= 0 ? "+" + r.ToString(f) : r.ToString(f);
    public static double Db(double v) => v <= 1e-6 ? -120 : 20 * Math.Log10(v);
}

static class Devices
{
    static readonly PropertyKey EnumeratorName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 24);
    static readonly PropertyKey DeviceFormat = new(new Guid("f19f064d-082c-4e27-bc73-6882a1bb8e4c"), 0);
    static readonly string[] FormFactors = ["RemoteNetworkDevice", "Speakers", "LineLevel", "Headphones", "Microphone", "Headset", "Handset", "UnknownDigitalPassthrough", "SPDIF", "DigitalAudioDisplayDevice", "UnknownFormFactor"];

    static string Prop(MMDevice d, PropertyKey k)
    {
        try { return d.Properties.Contains(k) ? d.Properties[k].Value?.ToString() ?? "null" : "n/a"; }
        catch (Exception ex) { return $"err {ex.GetType().Name}"; }
    }

    public static string KeyName(PropertyKey k) =>
        k.formatId == DeviceFormat.formatId && k.propertyId == 0 ? "PKEY_AudioEngine_DeviceFormat (device format changed)" : $"{{{k.formatId}}},{k.propertyId}";

    public static void List(string title, List<MMDevice> list, DataFlow flow, MMDeviceEnumerator en)
    {
        string Def(Role r) { try { return en.GetDefaultAudioEndpoint(flow, r).ID; } catch { return null; } }
        string dc = Def(Role.Console), dm = Def(Role.Communications);
        Log.W("");
        Log.W($"{title}:");
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            string ff = Prop(d, PropertyKeys.PKEY_AudioEndpoint_FormFactor);
            if (int.TryParse(ff, out var ffi) && ffi >= 0 && ffi < FormFactors.Length) ff = FormFactors[ffi];
            string mix;
            try { using var ac = d.CreateAudioClient(); mix = Fmt.Describe(ac.MixFormat); } catch (Exception ex) { mix = $"error {ex.Message}"; }
            Log.W($"  {i + 1}. {d.FriendlyName}{(d.ID == dc ? "  [default]" : "")}{(d.ID == dm ? "  [default communications]" : "")}");
            Log.W($"       mix format: {mix}");
            Log.W($"       form factor: {ff}, bus: {Prop(d, EnumeratorName)}");
        }
    }
}

static class Post
{
    public static int ReadFull(Stream r, byte[] buf)
    {
        int total = 0, got;
        while (total < buf.Length && (got = r.Read(buf, total, buf.Length - total)) > 0) total += got;
        return total;
    }

    // Mix without Leveling: L = Input + Output L, R = Input + Output R, 16-bit stereo 48 kHz.
    public static void Mix(string working, string mixPath)
    {
        long clipped = 0, frames = 0;
        using (var r = new WaveFileReader(working))
        using (var w = new WaveFileWriter(mixPath, new WaveFormat(Eng.Rate, 16, 2)))
        {
            var buf = new byte[6 * 4800];
            var o = new short[2 * 4800];
            int got;
            while ((got = ReadFull(r, buf)) > 0)
            {
                var s = MemoryMarshal.Cast<byte, short>(buf.AsSpan(0, got - got % 6));
                int n = s.Length / 3;
                for (int i = 0; i < n; i++)
                    for (int c = 0; c < 2; c++)
                    {
                        int v = s[3 * i] + s[3 * i + 1 + c];
                        if (v > short.MaxValue || v < short.MinValue) clipped++;
                        o[2 * i + c] = (short)Math.Clamp(v, short.MinValue, short.MaxValue);
                    }
                w.WriteSamples(o, 0, n * 2);
                frames += n;
            }
        }
        Log.W($"Mix (no Leveling): mix.wav, {frames / (double)Eng.Rate:F1} s, clipped samples {clipped}");
    }

    // Echo of the Output inside the Input: normalized cross-correlation on 8 kHz decimated copies,
    // on the loudest 5 s windows of the Output. Positive delay = the Input hears the Output later.
    public static void Echo(string working, double winS)
    {
        const int D = 6, R8 = Eng.Rate / D, LagMin = -R8 / 5, LagMax = R8 / 2;
        int Seg = (int)(winS * R8);
        var inD = new List<float>(); var outD = new List<float>();
        using (var r = new WaveFileReader(working))
        {
            var buf = new byte[6 * D * 4096];
            int got;
            while ((got = ReadFull(r, buf)) > 0)
            {
                var s = MemoryMarshal.Cast<byte, short>(buf.AsSpan(0, got - got % (6 * D)));
                for (int i = 0; i + 3 * D <= s.Length; i += 3 * D)
                {
                    float a = 0, b = 0;
                    for (int k = 0; k < D; k++) { a += s[i + 3 * k]; b += s[i + 3 * k + 1] + s[i + 3 * k + 2]; }
                    inD.Add(a / (D * 32768f)); outD.Add(b / (2 * D * 32768f));
                }
            }
        }
        var x = inD.ToArray(); var y = outD.ToArray();
        var cands = new List<(int S0, double Rms)>();
        for (int s0 = LagMax; s0 + Seg - LagMin <= y.Length; s0 += Seg)
        {
            double e = 0; for (int t = s0; t < s0 + Seg; t++) e += y[t] * y[t];
            double rms = Math.Sqrt(e / Seg);
            if (Fmt.Db(rms) > -45) cands.Add((s0, rms));
        }
        Log.W("");
        Log.W($"Echo estimate (Output inside Input), loudest {winS} s windows of the Output:");
        if (cands.Count == 0) { Log.W("  no window with Output above -45 dBFS RMS: nothing to correlate"); return; }
        var found = new List<(double Ms, double Ncc, double GainDb)>();
        foreach (var (s0, rms) in cands.OrderByDescending(c => c.Rms).Take(Math.Max(6, (int)(30 / winS))).OrderBy(c => c.S0))
        {
            double ex = 0; for (int t = s0; t < s0 + Seg; t++) ex += x[t] * x[t];
            var ncc = new double[LagMax - LagMin + 1]; var gain = new double[ncc.Length];
            Parallel.For(LagMin, LagMax + 1, lag =>
            {
                double xy = 0, yy = 0;
                for (int t = s0; t < s0 + Seg; t++) { float v = y[t - lag]; xy += x[t] * v; yy += v * v; }
                ncc[lag - LagMin] = ex > 0 && yy > 0 ? xy / Math.Sqrt(ex * yy) : 0;
                gain[lag - LagMin] = yy > 0 ? xy / yy : 0;
            });
            int best = 0; for (int i = 1; i < ncc.Length; i++) if (Math.Abs(ncc[i]) > Math.Abs(ncc[best])) best = i;
            double ms = (best + LagMin) * 1000.0 / R8, g = Fmt.Db(Math.Abs(gain[best]));
            Log.W($"  at {s0 / (double)R8,7:F1} s: Output RMS {Fmt.Db(rms),5:F1} dBFS, Input RMS {Fmt.Db(Math.Sqrt(ex / Seg)),5:F1} dBFS -> delay {ms,6:F1} ms, NCC {ncc[best]:+0.00;-0.00}, echo gain {g,5:F1} dB");
            found.Add((ms, ncc[best], g));
        }
        var good = found.Where(f => Math.Abs(f.Ncc) >= 0.2).ToList();
        if (good.Count == 0) Log.W("  |NCC| < 0.2 everywhere: no meaningful echo of the Output in the Input");
        else
        {
            double Med(IEnumerable<double> v) { var a = v.OrderBy(q => q).ToArray(); return a[a.Length / 2]; }
            Log.W($"  => echo on {good.Count}/{found.Count} windows: median delay {Med(good.Select(f => f.Ms)):F1} ms, median echo gain {Med(good.Select(f => f.GainDb)):F1} dB (relative to the Loopback capture level)");
        }
    }

    public static void Mp3(string mixPath, string mp3Path)
    {
        try
        {
            MediaFoundationApi.Startup();
            var sw = Stopwatch.StartNew();
            double secs;
            using (var r = new WaveFileReader(mixPath)) { secs = r.TotalTime.TotalSeconds; MediaFoundationEncoder.EncodeToMp3(r, mp3Path, 128000); }
            sw.Stop();
            Log.W($"MP3 via Media Foundation: mix.mp3 {new FileInfo(mp3Path).Length / 1024.0:F0} KB, encoded in {sw.Elapsed.TotalSeconds:F2} s = {sw.Elapsed.TotalSeconds / Math.Max(secs / 60, 1e-9):F2} s per minute of audio");
        }
        catch (Exception ex) { Log.W($"!! MP3 via Media Foundation failed: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}"); }
    }
}

static class Log
{
    static readonly object Gate = new();
    static readonly List<string> Pending = [];
    static StreamWriter file;
    public static void W(string s)
    {
        lock (Gate)
        {
            Console.WriteLine(s);
            if (file != null) file.WriteLine(s); else Pending.Add(s);
        }
    }
    public static void Open(string path)
    {
        lock (Gate) { file = new StreamWriter(path) { AutoFlush = true }; Pending.ForEach(file.WriteLine); Pending.Clear(); }
    }
    public static void Close() { lock (Gate) { file?.Dispose(); file = null; } }
}

static class Ask
{
    static string Line(string q, string def) { Console.Write($"{q} [{def}]: "); var s = Console.ReadLine()?.Trim(); return string.IsNullOrEmpty(s) ? def : s; }
    public static int Int(string q, int def, int min, int max)
    {
        while (true) if (int.TryParse(Line(q, def.ToString()), out var v) && v >= min && v <= max) return v;
    }
    public static string Str(string q, string def) => Line(q, def);
    public static bool YesNo(string q, bool def) => Line(q + " (y/n)", def ? "y" : "n").StartsWith("y", StringComparison.OrdinalIgnoreCase);
}

sealed class Options
{
    public bool ListOnly, NoEcho;
    public bool? Mp3;
    public int? In, Out, Seconds;
    public string Label, Fill = "stamp", InputMode = "default";
    public string Analyze;
    public double WinS = 5;
    public int LatencyMs = 250, TolMs = 10, GapMs = 50, BufferMs = 100;

    public static Options Parse(string[] a)
    {
        var o = new Options();
        for (int i = 0; i < a.Length; i++)
            switch (a[i])
            {
                case "--list": o.ListOnly = true; break;
                case "--in": o.In = int.Parse(a[++i]); break;
                case "--out": o.Out = int.Parse(a[++i]); break;
                case "--seconds": o.Seconds = int.Parse(a[++i]); break;
                case "--label": o.Label = a[++i]; break;
                case "--mp3": o.Mp3 = true; break;
                case "--no-mp3": o.Mp3 = false; break;
                case "--no-echo": o.NoEcho = true; break;
                case "--fill": o.Fill = a[++i]; break;
                case "--latency": o.LatencyMs = int.Parse(a[++i]); break;
                case "--tol": o.TolMs = int.Parse(a[++i]); break;
                case "--gap": o.GapMs = int.Parse(a[++i]); break;
                case "--buffer": o.BufferMs = int.Parse(a[++i]); break;
                case "--raw": o.InputMode = "raw"; break;
                case "--analyze": o.Analyze = a[++i]; break;
                case "--win": o.WinS = double.Parse(a[++i], CultureInfo.InvariantCulture); break;
                case "--comms": o.InputMode = "comms"; break;
                default: throw new ArgumentException($"unknown option {a[i]}");
            }
        return o;
    }
}

static class Paths
{
    public static string ProjectDir([CallerFilePath] string p = "") => Path.GetDirectoryName(p);
}
