using Duettino.Selection;

namespace Duettino.Tests;

public sealed class SettingsTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "duettino-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    string SettingsPath => Path.Combine(folder, "Duettino", "settings.json");

    [Fact]
    public void The_chosen_Input_Output_and_destination_folder_survive_a_restart()
    {
        var saved = new Settings(
            new AudioEndpoint("{0.0.1.00000000}.{headset-mic}", "Headset Microphone"),
            new AudioEndpoint("{0.0.0.00000000}.{headphones}", "Headphones"),
            @"D:\Calls");

        saved.Save(SettingsPath);

        Assert.Equal(saved, Settings.Load(SettingsPath));
    }

    [Fact]
    public void Without_a_settings_file_nothing_is_remembered_and_Recording_files_go_to_Documents_Duettino()
    {
        var settings = Settings.Load(SettingsPath);

        Assert.Null(settings.Input);
        Assert.Null(settings.Output);
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Duettino"),
            settings.Folder);
    }

    [Fact]
    public void A_remembered_device_is_kept_by_its_ID_even_without_a_name()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "Input": { "Id": "{headset-mic}" }, "Folder": "D:\\Calls" }""");

        Assert.Equal("{headset-mic}", Settings.Load(SettingsPath).Input?.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    [InlineData("""{ "Input": { "Name": "no id" }, "Folder": 42 }""")]
    [InlineData("""{ "Folder": "" }""")]
    public void A_corrupt_settings_file_falls_back_to_the_defaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);

        Assert.Equal(Settings.Load(Path.Combine(folder, "missing.json")), Settings.Load(SettingsPath));
    }
}
