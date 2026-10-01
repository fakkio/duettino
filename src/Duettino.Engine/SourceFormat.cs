namespace Duettino.Engine;

public enum SampleType
{
    Pcm16,
    Float32,
}

/// <summary>The format of the interleaved samples a Source delivers.</summary>
public readonly record struct SourceFormat(int SampleRate, int Channels, SampleType SampleType);

[Flags]
public enum PacketFlags
{
    None = 0,

    /// <summary>Windows lost audio just before this packet.</summary>
    DataDiscontinuity = 1,

    /// <summary>The packet is silence, whatever its bytes hold.</summary>
    Silent = 2,
}
