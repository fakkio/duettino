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
    internal const string PartialSuffix = ".partial";

    /// <summary>
    /// Levels and mixes the Working file at <paramref name="workingFilePath"/> and encodes it next to it, named after it
    /// (<c>X.working.wav</c> → <c>X.mp3</c>, or <c>X_2.mp3</c>… if taken), then deletes the Working file.
    /// Without an MP3 encoder the Recording file is a stereo WAV (<c>X.wav</c>) instead (ADR-0003).
    /// If this fails or is cancelled, the Working file is kept and no partial Recording file is left behind.
    /// The encoding goes into <c>X.mp3.partial</c>, renamed only once complete, so even a process killed midway never
    /// leaves a file that looks like a Recording file; finalizing the Working file again replaces it.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public static FinalizationResult Run(string workingFilePath, IMp3Encoder encoder, CancellationToken cancellation = default)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(workingFilePath))!;
        var name = Path.GetFileName(workingFilePath);
        var stem = name.EndsWith(Recording.WorkingFileSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^Recording.WorkingFileSuffix.Length]
            : Path.GetFileNameWithoutExtension(name);

        FinalizationResult result;
        try
        {
            result = new(WriteMix(workingFilePath, folder, stem, ".mp3", encoder.Encode, cancellation), IsWav: false);
        }
        catch (Mp3EncoderUnavailableException)
        {
            result = new(WriteMix(workingFilePath, folder, stem, ".wav", WriteWav, cancellation), IsWav: true);
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
    static string WriteMix(
        string workingFilePath, string folder, string stem, string extension, Action<Stream, FileStream> encode,
        CancellationToken cancellation)
    {
        using var mix = new MixStream(workingFilePath, cancellation);
        var partialPath = Path.Combine(folder, stem + extension + PartialSuffix);
        try
        {
            using (var destination = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
                encode(mix, destination);
            return MoveToFree(partialPath, folder, stem, extension);
        }
        catch
        {
            try
            {
                File.Delete(partialPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still held: the original error matters more. It is replaced or swept away later (Recovery).
            }
            throw;
        }
    }

    static void WriteWav(Stream mix, FileStream destination)
    {
        using var writer = new WavFileWriter(destination, Recording.Rate, channels: 2);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = mix.Read(buffer)) > 0) writer.Write(MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, read)));
    }

    /// <summary>
    /// Renames <paramref name="path"/> to <c>stem.ext</c>, or the first free of <c>stem_2.ext</c>, <c>stem_3.ext</c>…,
    /// never overwriting; returns the new path.
    /// </summary>
    static string MoveToFree(string path, string folder, string stem, string extension)
    {
        for (int n = 1; ; n++)
        {
            var destination = Path.Combine(folder, (n == 1 ? stem : $"{stem}_{n}") + extension);
            try
            {
                File.Move(path, destination, overwrite: false);
                return destination;
            }
            catch (IOException) when (File.Exists(destination))
            {
            }
        }
    }
}
