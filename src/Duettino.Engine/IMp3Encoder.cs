namespace Duettino.Engine;

/// <summary>Turns the Mix into the MP3 Recording file (ADR-0003).</summary>
public interface IMp3Encoder
{
    /// <summary>
    /// Encodes <paramref name="mix"/>, raw 16-bit stereo 48 kHz PCM read to its end, as an MP3 into
    /// <paramref name="destination"/>.
    /// </summary>
    /// <exception cref="Mp3EncoderUnavailableException">This machine has no MP3 encoder; nothing was written.</exception>
    void Encode(Stream mix, Stream destination);
}

/// <summary>Windows has no MP3 encoder, as on N editions without the Media Feature Pack.</summary>
public sealed class Mp3EncoderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
