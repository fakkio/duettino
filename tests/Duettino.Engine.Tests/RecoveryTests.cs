using System.Buffers.Binary;

namespace Duettino.Engine.Tests;

public sealed class RecoveryTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "duettino-tests", Guid.NewGuid().ToString("N"));

    public RecoveryTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    string PathOf(string name) => Path.Combine(folder, name);

    /// <summary>
    /// Leaves a Working file as a crash does: <paramref name="frames"/> frames on disk, a header still counting the
    /// <paramref name="headerFrames"/> written at its last update, and half a frame the crash cut short.
    /// </summary>
    void WriteCrashed(string path, int frames, int headerFrames, Func<int, short> input)
    {
        WorkingFiles.Write(path, frames, i => (input(i), 0, 0));
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)(36 + headerFrames * 6));
        file.Position = 4;
        file.Write(size);
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)(headerFrames * 6));
        file.Position = 40;
        file.Write(size);
        file.Seek(0, SeekOrigin.End);
        file.Write([1, 2, 3]);
    }

    [Fact]
    public void Recovering_a_Working_file_whose_header_lags_its_data_keeps_all_the_audio_on_disk()
    {
        // The header was last updated a second before the crash; 1.5 s of audio reached the disk.
        var path = PathOf("Duettino_2026-10-01_14-30-05.working.wav");
        WriteCrashed(path, frames: 72000, headerFrames: 24000, input: i => (short)(i < 70000 ? 0 : 8000));
        var encoder = new CapturingEncoder();

        var result = Finalization.Run(path, encoder);

        Assert.Equal(PathOf("Duettino_2026-10-01_14-30-05.mp3"), result.RecordingFilePath);
        Assert.Equal(72000 * 2, encoder.Mix.Length);
        Assert.Contains(encoder.Mix.AsSpan(2 * 70000).ToArray(), s => s != 0); // the last half second isn't lost
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void The_Orphan_Working_files_in_a_folder_are_found_oldest_first_with_their_length_on_disk()
    {
        WriteCrashed(PathOf("Duettino_2026-10-02_09-00-00.working.wav"), frames: 72000, headerFrames: 0, input: _ => 0);
        WorkingFiles.Write(PathOf("Duettino_2026-10-01_14-30-05.working.wav"), 4800, _ => (0, 0, 0));
        File.WriteAllText(PathOf("Duettino_2026-10-01_14-30-05.mp3"), "a Recording file");
        File.WriteAllText(PathOf("notes.wav"), "something else");

        var orphans = Recovery.FindOrphans(folder);

        Assert.Equal(
            [
                new OrphanWorkingFile(PathOf("Duettino_2026-10-01_14-30-05.working.wav"), TimeSpan.FromSeconds(0.1)),
                new OrphanWorkingFile(PathOf("Duettino_2026-10-02_09-00-00.working.wav"), TimeSpan.FromSeconds(1.5)),
            ],
            orphans);
    }

    [Fact]
    public void A_folder_that_does_not_exist_yet_has_no_Orphan_Working_files()
    {
        Assert.Empty(Recovery.FindOrphans(PathOf("never-recorded-here")));
    }

    [Fact]
    public void The_Working_file_of_a_Recording_still_running_is_not_an_Orphan()
    {
        // As in another Duettino window recording into the same folder.
        var clock = new FakeClock(new DateTime(2026, 10, 1, 14, 30, 5));
        using (var recording = Recording.Start(folder, clock))
        {
            clock.AdvanceMs(2000);
            recording.Advance();

            Assert.Empty(Recovery.FindOrphans(folder));
        }

        Assert.Single(Recovery.FindOrphans(folder));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(44)]
    public void A_Working_file_cut_short_before_any_audio_is_found_with_no_length(int bytes)
    {
        // The crash came before the header reached the disk, or right after it.
        var path = PathOf("Duettino_2026-10-01_14-30-05.working.wav");
        WorkingFiles.Write(path, 4800, _ => (0, 0, 0));
        using (var file = new FileStream(path, FileMode.Open)) file.SetLength(bytes);

        Assert.Equal([new OrphanWorkingFile(path, TimeSpan.Zero)], Recovery.FindOrphans(folder));
    }

    [Fact]
    public void An_unreadable_Working_file_is_still_offered_with_an_unknown_length()
    {
        // The user decides what to do with it: Recover will fail and keep it, Delete removes it.
        File.WriteAllText(PathOf("Duettino_2026-10-01_14-30-05.working.wav"), "RIFF");

        Assert.Equal([new OrphanWorkingFile(PathOf("Duettino_2026-10-01_14-30-05.working.wav"), null)], Recovery.FindOrphans(folder));
    }
}
