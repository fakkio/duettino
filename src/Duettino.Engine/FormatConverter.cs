using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Duettino.Engine;

/// <summary>
/// Brings one Source's packets to the Working file's layout (48 kHz float, Input mono, Output stereo) and places
/// them on its timeline. All in managed code, never with Media Foundation (ADR-0003).
/// </summary>
/// <remarks>
/// Every packet goes through a resampler, even at 48 kHz, which carries over from one packet to the next for as long
/// as they follow on from each other, its ratio nudged so that the Source's audio keeps up with its timestamps however
/// its clock drifts (<see cref="DriftLoop"/>). When they don't follow on (a loss Windows reports, a jump of
/// <see cref="Tolerance"/> or more, a Gap, a device switch to another format), the audio held back by the resampler
/// goes where the previous packet ended and resampling starts afresh exactly at the new packet's timestamp; the
/// held-back audio goes there too when the Source stays quiet until the Working file is about to be written at that
/// point. A smaller jump Windows doesn't report is taken back gradually, like drift: no audio is cut out for it.
/// Thread-safe: packets of the old and the new device may overlap while a Source switches device.
/// </remarks>
sealed class FormatConverter(SourceTimeline timeline)
{
    /// <summary>
    /// A packet whose timestamp is less than this many frames from where the Source's audio ends carries on from it,
    /// so timestamp jitter doesn't chop the audio and drift is taken back gradually; beyond it, audio was lost and
    /// the packet goes exactly where its timestamp says.
    /// </summary>
    const int Tolerance = Recording.Rate / 100;

    readonly object gate = new();
    readonly object peakGate = new();
    readonly int targetChannels = timeline.Channels;
    readonly DriftLoop drift = new();
    SourceFormat? format;
    Resampler? resampler; // null until a packet starts the Source's audio afresh
    long resamplerStart;  // timeline position of the resampler's first input frame
    float peak;

    /// <summary>Converts one packet whose first frame belongs at timeline <paramref name="position"/>, and places it.</summary>
    public void Deliver(SourceFormat packetFormat, ReadOnlySpan<byte> data, PacketFlags flags, long position)
    {
        var samples = ToTargetChannels(packetFormat, data, flags);
        // An empty packet carries no audio, and its timestamp can be garbage (NAudio hands one over, stamped 0, after
        // every real packet): placing it would lose track of where the Source's audio ends.
        if (samples.Length == 0) return;
        float packetPeak = 0;
        foreach (var sample in samples) packetPeak = Math.Max(packetPeak, Math.Abs(sample));
        lock (peakGate) peak = Math.Max(peak, packetPeak);
        lock (gate)
        {
            if (packetFormat != format)
            {
                ReleaseHeld();
                format = packetFormat;
                drift.ForgetDrift();
            }
            else if (resampler != null)
            {
                // How far the packet's timestamp is ahead of where the Source's audio ends.
                double lag = position - (resamplerStart + resampler.InputEnd);
                if (flags.HasFlag(PacketFlags.DataDiscontinuity) || Math.Abs(lag) >= Tolerance)
                    ReleaseHeld();
                else
                    resampler.Correction = drift.Update(lag, (double)samples.Length / targetChannels / packetFormat.SampleRate);
            }
            if (resampler == null)
            {
                resampler = new Resampler(packetFormat.SampleRate, targetChannels) { Correction = drift.Reanchor() };
                resamplerStart = position;
            }
            Place(resampler.Push(samples, out long start), resamplerStart + start);
        }
    }

    /// <summary>The highest absolute sample delivered since the last call, in the target channels; 0 if none.</summary>
    public float TakePeak()
    {
        lock (peakGate)
        {
            float taken = peak;
            peak = 0;
            return taken;
        }
    }

    /// <summary>
    /// Places the audio the resampler holds back if it belongs before timeline <paramref name="position"/>, which the
    /// Working file is about to be written up to: the Source has gone quiet (a Gap) and no packet will release it.
    /// </summary>
    public void ReleaseHeldBefore(long position)
    {
        lock (gate)
        {
            if (resampler != null && resamplerStart + resampler.Emitted < position) ReleaseHeld();
        }
    }

    /// <summary>Places the audio the resampler holds back where the Source's audio ends: the next packet starts afresh.</summary>
    void ReleaseHeld()
    {
        if (resampler != null) Place(resampler.Flush(out long start), resamplerStart + start);
        resampler = null;
    }

    void Place(float[] samples, long position)
    {
        if (samples.Length > 0) timeline.Place(samples, position);
    }

    /// <summary>The packet's frames as float in the target channels, still at the Source's sample rate.</summary>
    float[] ToTargetChannels(SourceFormat format, ReadOnlySpan<byte> data, PacketFlags flags)
    {
        int bytesPerSample = format.SampleType == SampleType.Float32 ? sizeof(float) : sizeof(short);
        int frames = data.Length / (bytesPerSample * format.Channels);
        var result = new float[frames * targetChannels];
        if (flags.HasFlag(PacketFlags.Silent)) return result;

        var samples = ToFloat(format.SampleType, data[..(frames * format.Channels * bytesPerSample)]);
        if (targetChannels == 1)
        {
            // The Input: whatever its channels (a stereo microphone, an array), their average.
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                foreach (var s in samples.AsSpan(i * format.Channels, format.Channels)) sum += s;
                result[i] = sum / format.Channels;
            }
        }
        else
        {
            var gains = StereoGains(format);
            for (int i = 0; i < frames; i++)
            {
                var frame = samples.AsSpan(i * format.Channels, format.Channels);
                for (int c = 0; c < frame.Length; c++)
                {
                    result[2 * i] += gains[c].Left * frame[c];
                    result[2 * i + 1] += gains[c].Right * frame[c];
                }
            }
        }
        return result;
    }

    /// <summary>
    /// How much of each channel goes to the left and the right of the Output: a mono channel to both, then by
    /// speaker as in ITU-R BS.775, the front pair as is, centers and surrounds at -3 dB, the LFE left out; scaled
    /// down when a side adds up to more than full scale, so a loud surround mix can't clip.
    /// </summary>
    static (float Left, float Right)[] StereoGains(SourceFormat format)
    {
        const float Full = 1, Minus3dB = 0.70710677f;
        var gains = new (float Left, float Right)[format.Channels];
        if (format.Channels == 1)
        {
            gains[0] = (Full, Full);
            return gains;
        }

        int channel = 0;
        var mask = (int)(format.ChannelMask != Speakers.None ? format.ChannelMask : DefaultLayout(format.Channels));
        for (int bit = 0; bit < 31 && channel < gains.Length; bit++)
        {
            var speaker = (Speakers)(1 << bit);
            if ((mask & (int)speaker) == 0) continue;
            gains[channel++] = speaker switch
            {
                Speakers.FrontLeft or Speakers.FrontLeftOfCenter => (Full, 0),
                Speakers.FrontRight or Speakers.FrontRightOfCenter => (0, Full),
                Speakers.LowFrequency => (0, 0),
                Speakers.BackLeft or Speakers.SideLeft or Speakers.TopFrontLeft or Speakers.TopBackLeft => (Minus3dB, 0),
                Speakers.BackRight or Speakers.SideRight or Speakers.TopFrontRight or Speakers.TopBackRight => (0, Minus3dB),
                _ => (Minus3dB, Minus3dB), // the centers: front, back, top
            };
        }
        float scale = Math.Min(1, 1 / Math.Max(gains.Sum(g => g.Left), gains.Sum(g => g.Right)));
        for (int c = 0; c < gains.Length; c++) gains[c] = (gains[c].Left * scale, gains[c].Right * scale);
        return gains; // channels beyond the mask's speakers stay out
    }

    /// <summary>The speakers Windows assumes for a format declared without a channel mask.</summary>
    static Speakers DefaultLayout(int channels) => channels switch
    {
        2 => Speakers.FrontLeft | Speakers.FrontRight,
        4 => Speakers.FrontLeft | Speakers.FrontRight | Speakers.BackLeft | Speakers.BackRight,
        6 => Speakers.FrontLeft | Speakers.FrontRight | Speakers.FrontCenter | Speakers.LowFrequency | Speakers.BackLeft | Speakers.BackRight,
        8 => Speakers.FrontLeft | Speakers.FrontRight | Speakers.FrontCenter | Speakers.LowFrequency | Speakers.BackLeft | Speakers.BackRight | Speakers.SideLeft | Speakers.SideRight,
        _ => (Speakers)((1L << Math.Min(channels, 31)) - 1), // the speakers in bit order, from the front left on
    };

    static float[] ToFloat(SampleType type, ReadOnlySpan<byte> data)
    {
        if (type == SampleType.Float32) return MemoryMarshal.Cast<byte, float>(data).ToArray();
        var pcm = MemoryMarshal.Cast<byte, short>(data);
        var result = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) result[i] = pcm[i] / 32768f;
        return result;
    }
}
