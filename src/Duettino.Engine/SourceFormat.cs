using NAudio.Wave;

namespace Duettino.Engine;

public enum SampleType
{
    Pcm16,
    Float32,
}

/// <summary>
/// The format of the interleaved samples a Source delivers. <paramref name="ChannelMask"/> gives the speaker of each
/// channel, in the order of its bits; <see cref="Speakers.None"/> means Windows' default layout for the channel count.
/// </summary>
public readonly record struct SourceFormat(int SampleRate, int Channels, SampleType SampleType, Speakers ChannelMask = Speakers.None)
{
    /// <summary>Reads a capture format, whether Windows declares it plain or Extensible.</summary>
    /// <exception cref="NotSupportedException">The samples are neither 16-bit PCM nor 32-bit float.</exception>
    public static SourceFormat From(WaveFormat format)
    {
        var extensible = format as WaveFormatExtensible;
        var subFormat = extensible?.SubFormat ?? Guid.Empty;
        var mask = (Speakers)(extensible?.ChannelMask ?? 0);
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat || subFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT;
        bool isPcm = format.Encoding == WaveFormatEncoding.Pcm || subFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM;
        if (isFloat && format.BitsPerSample == 32) return new SourceFormat(format.SampleRate, format.Channels, SampleType.Float32, mask);
        if (isPcm && format.BitsPerSample == 16) return new SourceFormat(format.SampleRate, format.Channels, SampleType.Pcm16, mask);
        throw new NotSupportedException($"{format.Encoding}, {format.BitsPerSample}-bit, {format.SampleRate} Hz, {format.Channels} channels is not supported.");
    }
}

[Flags]
public enum PacketFlags
{
    None = 0,

    /// <summary>Windows lost audio just before this packet.</summary>
    DataDiscontinuity = 1,

    /// <summary>The packet is silence, whatever its bytes hold.</summary>
    Silent = 2,
}
