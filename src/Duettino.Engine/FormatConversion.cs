using System.Runtime.InteropServices;

namespace Duettino.Engine;

/// <summary>Brings a Source packet to the Working file's layout: 48 kHz float, Input mono, Output stereo.</summary>
static class FormatConversion
{
    public static float[] Convert(SourceFormat format, ReadOnlySpan<byte> data, PacketFlags flags, int targetChannels)
    {
        if (format.SampleRate != Recording.Rate)
            throw new NotSupportedException($"Sources at {format.SampleRate} Hz are not supported yet, only {Recording.Rate} Hz.");

        int bytesPerSample = format.SampleType == SampleType.Float32 ? sizeof(float) : sizeof(short);
        int frames = data.Length / (bytesPerSample * format.Channels);
        var result = new float[frames * targetChannels];
        if (flags.HasFlag(PacketFlags.Silent)) return result;

        var samples = ToFloat(format.SampleType, data[..(frames * format.Channels * bytesPerSample)]);
        for (int i = 0; i < frames; i++)
        {
            var frame = samples.AsSpan(i * format.Channels, format.Channels);
            if (targetChannels == 1)
            {
                float sum = 0;
                foreach (var s in frame) sum += s;
                result[i] = sum / frame.Length;
            }
            else
            {
                result[2 * i] = frame[0];
                result[2 * i + 1] = frame[frame.Length > 1 ? 1 : 0];
            }
        }
        return result;
    }

    static float[] ToFloat(SampleType type, ReadOnlySpan<byte> data)
    {
        if (type == SampleType.Float32) return MemoryMarshal.Cast<byte, float>(data).ToArray();
        var pcm = MemoryMarshal.Cast<byte, short>(data);
        var result = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) result[i] = pcm[i] / 32768f;
        return result;
    }
}
