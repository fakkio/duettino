using Duettino.Engine;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Duettino.Devices;

/// <summary>
/// Captures one Source from its endpoint with a <see cref="WasapiRecorder"/> (Loopback capture for the Output)
/// and hands every packet to the Recording, stamped with its capture time.
/// </summary>
sealed class SourceCapture : IDisposable
{
    const int BufferMs = 100;

    readonly Source source;
    readonly WasapiRecorder recorder;
    readonly SourceFormat format;
    Recording? recording;
    int failed;

    public SourceCapture(Source source, string endpointId)
    {
        this.source = source;
        using var enumerator = new MMDeviceEnumerator();
        var builder = new WasapiRecorderBuilder().WithDevice(enumerator.GetDevice(endpointId)).WithBufferLength(BufferMs);
        if (source == Source.Output) builder = builder.WithLoopbackCapture();
        recorder = builder.Build();
        try
        {
            format = ToSourceFormat(recorder.WaveFormat);
        }
        catch
        {
            recorder.Dispose();
            throw;
        }
        recorder.DataAvailable += OnDataAvailable;
        recorder.RecordingStopped += (_, e) =>
        {
            if (e.Exception != null) Fail(e.Exception);
        };
    }

    /// <summary>Raised, once, when the capture can't go on; the Source is then recorded as silence.</summary>
    public event Action<Source, Exception>? Failed;

    public void Start(Recording target)
    {
        recording = target;
        recorder.StartRecording();
    }

    public void Dispose()
    {
        recorder.DataAvailable -= OnDataAvailable;
        recorder.Dispose();
    }

    void OnDataAvailable(ReadOnlySpan<byte> data, AudioClientBufferFlags flags, long devicePosition, long captureTime)
    {
        if (recording == null || Volatile.Read(ref failed) != 0) return;
        try
        {
            recording.Deliver(source, format, data, captureTime, ToPacketFlags(flags));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    void Fail(Exception ex)
    {
        if (Interlocked.Exchange(ref failed, 1) == 0) Failed?.Invoke(source, ex);
    }

    static PacketFlags ToPacketFlags(AudioClientBufferFlags flags) =>
        (flags.HasFlag(AudioClientBufferFlags.DataDiscontinuity) ? PacketFlags.DataDiscontinuity : PacketFlags.None) |
        (flags.HasFlag(AudioClientBufferFlags.Silent) ? PacketFlags.Silent : PacketFlags.None);

    /// <summary>Reads the capture format, whether Windows declares it plain or Extensible.</summary>
    static SourceFormat ToSourceFormat(WaveFormat f)
    {
        var subFormat = f is WaveFormatExtensible x ? x.SubFormat : Guid.Empty;
        bool isFloat = f.Encoding == WaveFormatEncoding.IeeeFloat || subFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT;
        bool isPcm = f.Encoding == WaveFormatEncoding.Pcm || subFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM;
        if (isFloat && f.BitsPerSample == 32) return new SourceFormat(f.SampleRate, f.Channels, SampleType.Float32);
        if (isPcm && f.BitsPerSample == 16) return new SourceFormat(f.SampleRate, f.Channels, SampleType.Pcm16);
        throw new NotSupportedException($"{f.Encoding}, {f.BitsPerSample}-bit, {f.SampleRate} Hz, {f.Channels} channels is not supported.");
    }
}
