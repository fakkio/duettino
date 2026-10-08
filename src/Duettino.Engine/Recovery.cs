namespace Duettino.Engine;

/// <summary>A Working file whose Recording never reached Finalization.</summary>
/// <param name="Path">The Working file.</param>
/// <param name="Length">The audio on disk, or null if the file can't be read as a Working file.</param>
public sealed record OrphanWorkingFile(string Path, TimeSpan? Length);

/// <summary>
/// Finds the Orphan Working files a crash left behind. Recovering one is a plain <see cref="Finalization.Run"/>, which
/// takes the audio up to the end of the file, whatever its header says (ADR-0004).
/// </summary>
public static class Recovery
{
    /// <summary>
    /// The Working files in <paramref name="folder"/>, oldest first, leaving out those still being written by a
    /// running Recording.
    /// </summary>
    /// <exception cref="IOException">The folder can't be listed, as a network folder that is offline.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder can't be listed.</exception>
    public static IReadOnlyList<OrphanWorkingFile> FindOrphans(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var orphans = new List<OrphanWorkingFile>();
        // Named after their start time, so the name sorts them chronologically.
        foreach (var path in Directory.GetFiles(folder, "*" + Recording.WorkingFileSuffix).Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var reader = new WorkingFileReader(path);
                orphans.Add(new(path, TimeSpan.FromSeconds(reader.Frames / (double)Recording.Rate)));
            }
            catch (EndOfStreamException) when (new FileInfo(path).Length == 0)
            {
                // The crash came before even the header reached the disk.
                orphans.Add(new(path, TimeSpan.Zero));
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
            {
                orphans.Add(new(path, null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Open for writing elsewhere, as by a Recording running into it, or not readable now: it may be next time.
            }
        }
        return orphans;
    }

    /// <summary>
    /// Deletes the partial files that Finalizations killed midway left in <paramref name="folder"/> and that no
    /// Finalization will replace any more, because their Working file is gone (the user chose to delete it).
    /// Best effort: what can't be listed or deleted now is tried again next time.
    /// </summary>
    public static void DeleteStalePartialFiles(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (var path in Directory.GetFiles(folder, "*" + Finalization.PartialSuffix))
            {
                // X.mp3.partial or X.wav.partial belongs to X.working.wav.
                var stem = Path.GetFileNameWithoutExtension(path[..^Finalization.PartialSuffix.Length]);
                if (!File.Exists(Path.Combine(folder, stem + Recording.WorkingFileSuffix))) File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
