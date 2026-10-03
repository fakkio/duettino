using System.Runtime.InteropServices;

namespace Duettino.Engine;

/// <summary>What a Finalization produced.</summary>
/// <param name="RecordingFilePath">The Recording file.</param>
/// <param name="IsWav">
/// True when no MP3 encoder was available and the Recording file is a 16-bit stereo 48 kHz WAV instead.
/// </param>
public sealed record FinalizationResult(string RecordingFilePath, bool IsWav);

/// <summary>Turns a Working file into a Recording file: Leveling, Mix, encoding, then removing the Working file (ADR-0004).</summary>
public static class Finalization
{
    /// <summary>
    /// Levels and mixes the Working file at <paramref name="workingFilePath"/> and encodes it next to it, named after it
    /// (<c>X.working.wav</c> → <c>X.mp3</c>, or <c>X_2.mp3</c>… if taken), then deletes the Working file.
    /// Without an MP3 encoder the Recording file is a stereo WAV (<c>X.wav</c>) instead (ADR-0003).
    /// If this fails, the Working file is kept and no partial Recording file is left behind.
    /// </summary>
    public static FinalizationResult Run(string workingFilePath, IMp3Encoder encoder)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(workingFilePath))!;
        var name = Path.GetFileName(workingFilePath);
        var stem = name.EndsWith(Recording.WorkingFileSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^Recording.WorkingFileSuffix.Length]
            : Path.GetFileNameWithoutExtension(name);

        FinalizationResult result;
        try
        {
            result = new(WriteMix(workingFilePath, folder, stem, ".mp3", encoder.Encode), IsWav: false);
        }
        catch (Mp3EncoderUnavailableException)
        {
            result = new(WriteMix(workingFilePath, folder, stem, ".wav", WriteWav), IsWav: true);
        }

        try
        {
            File.Delete(workingFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The Recording file is complete: a Working file that can't be deleted yet (locked by an antivirus, say)
            // is only disk space, and it will be offered for Recovery or deletion at the next launch.
        }
        return result;
    }

    /// <summary>Writes the Mix through <paramref name="encode"/> into a new file; returns its path.</summary>
    static string WriteMix(string workingFilePath, string folder, string stem, string extension, Action<Stream, FileStream> encode)
    {
        using var mix = new MixStream(workingFilePath);
        var destination = CreateFree(folder, stem, extension, out var path);
        try
        {
            using (destination) encode(mix, destination);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
        return path;
    }

    static void WriteWav(Stream mix, FileStream destination)
    {
        using var writer = new WavFileWriter(destination, Recording.Rate, channels: 2);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = mix.Read(buffer)) > 0) writer.Write(MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, read)));
    }

    /// <summary>Creates <c>stem.ext</c>, or the first free of <c>stem_2.ext</c>, <c>stem_3.ext</c>…, never overwriting.</summary>
    static FileStream CreateFree(string folder, string stem, string extension, out string path)
    {
        for (int n = 1; ; n++)
        {
            path = Path.Combine(folder, (n == 1 ? stem : $"{stem}_{n}") + extension);
            try
            {
                return new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }
}
