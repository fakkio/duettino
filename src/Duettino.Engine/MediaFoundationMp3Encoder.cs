using System.Runtime.InteropServices;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace Duettino.Engine;

/// <summary>The MP3 encoder that ships with Windows, through Media Foundation, at 128 kbps (ADR-0003).</summary>
public sealed class MediaFoundationMp3Encoder : IMp3Encoder
{
    const int BitRate = 128_000;
    const int ClassNotRegistered = unchecked((int)0x80040154); // REGDB_E_CLASSNOTREG

    static readonly WaveFormat MixFormat = new(Recording.Rate, 16, 2);

    public void Encode(Stream mix, Stream destination)
    {
        using var mediaType = SelectMediaType();
        using var encoder = new MediaFoundationEncoder(mediaType);
        using var source = new RawSourceWaveStream(mix, MixFormat);
        encoder.Encode(destination, source, TranscodeContainerTypes.MFTranscodeContainerType_MP3);
    }

    /// <summary>Finds the MP3 media type the encoder writes for the Mix at 128 kbps, or tells that Windows has no MP3 encoder.</summary>
    static MediaType SelectMediaType()
    {
        try
        {
            MediaFoundationApi.Startup();
            return MediaFoundationEncoder.SelectMediaType(AudioSubtypes.MFAudioFormat_MP3, MixFormat, BitRate)
                ?? throw new Mp3EncoderUnavailableException("Windows has no MP3 encoder for 48 kHz stereo.");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
            or COMException { HResult: ClassNotRegistered })
        {
            // Media Foundation itself is missing, as on N editions without the Media Feature Pack. Any other error is a
            // real failure: reporting it as a missing encoder would send the user after a Media Feature Pack for nothing.
            throw new Mp3EncoderUnavailableException("Media Foundation is not available: " + ex.Message, ex);
        }
    }
}
