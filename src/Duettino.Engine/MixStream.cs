using System.Runtime.InteropServices;

namespace Duettino.Engine;

/// <summary>
/// The Mix of a Working file, computed as it is read: raw 16-bit stereo 48 kHz PCM, the Input on both sides added to
/// the Output. The sum can reach twice full scale (the Loopback capture reaches 0 dBFS), so a <see cref="Limiter"/>
/// keeps it under a ceiling with some headroom: no clipping, no wrap-around (ADR-0004).
/// </summary>
sealed class MixStream : Stream
{
    const int MixFrameBytes = 2 * sizeof(short);
    const int ChunkFrames = 4096;

    readonly WorkingFileReader reader;
    readonly Limiter limiter = new();
    readonly short[] workingChunk = new short[ChunkFrames * WorkingFileReader.Channels];
    int delayedFrames = Limiter.Delay;
    long position;

    public MixStream(string workingFilePath)
    {
        reader = new WorkingFileReader(workingFilePath);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => reader.Frames * MixFrameBytes;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int wanted = (int)Math.Min(buffer.Length / MixFrameBytes, (Length - position) / MixFrameBytes);
        var mix = MemoryMarshal.Cast<byte, short>(buffer[..(wanted * MixFrameBytes)]);
        int produced = 0;
        while (produced < wanted)
        {
            // Past the end of the Working file, silence pushes the limiter's last delayed frames out.
            int frames = reader.Read(workingChunk.AsSpan(0, Math.Min(wanted - produced, ChunkFrames) * WorkingFileReader.Channels));
            if (frames == 0)
            {
                frames = Math.Min(wanted - produced, ChunkFrames);
                workingChunk.AsSpan(0, frames * WorkingFileReader.Channels).Clear();
            }
            for (int i = 0; i < frames; i++)
            {
                var frame = workingChunk.AsSpan(i * WorkingFileReader.Channels, WorkingFileReader.Channels);
                float input = frame[0] / 32768f;
                var (left, right) = limiter.Process(input + frame[1] / 32768f, input + frame[2] / 32768f);
                if (delayedFrames > 0)
                {
                    delayedFrames--;
                    continue;
                }
                mix[2 * produced] = ToPcm16(left);
                mix[2 * produced + 1] = ToPcm16(right);
                produced++;
            }
        }
        position += produced * MixFrameBytes;
        return produced * MixFrameBytes;
    }

    static short ToPcm16(float sample) => (short)Math.Round(sample * 32768f);

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) reader.Dispose();
        base.Dispose(disposing);
    }
}
