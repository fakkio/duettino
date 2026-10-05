using System.Text.Json;
using Duettino.Engine;
using Duettino.Selection;

namespace Duettino;

/// <summary>
/// What Duettino remembers between runs: the chosen Input and Output, by endpoint ID (the name is only shown when the
/// endpoint is absent), and the destination folder.
/// </summary>
sealed record Settings(AudioEndpoint? Input, AudioEndpoint? Output, string Folder)
{
    /// <summary>The settings file, under %AppData%\Duettino.</summary>
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Duettino", "settings.json");

    static readonly string DefaultFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Duettino");

    /// <summary>Reads the settings; whatever is missing, unreadable or invalid falls back to its default.</summary>
    public static Settings Load(string path)
    {
        Settings? read;
        try
        {
            read = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            read = null;
        }
        return new Settings(
            ValidOrNull(read?.Input),
            ValidOrNull(read?.Output),
            string.IsNullOrWhiteSpace(read?.Folder) ? DefaultFolder : read.Folder);
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this));
    }

    /// <summary>The endpoint chosen for <paramref name="source"/>, if any.</summary>
    public AudioEndpoint? Chosen(Source source) => source == Source.Input ? Input : Output;

    public Settings WithChosen(Source source, AudioEndpoint endpoint) =>
        source == Source.Input ? this with { Input = endpoint } : this with { Output = endpoint };

    // Deserialization leaves a missing ID or name null, whatever the record's annotations say. The ID is what
    // identifies the endpoint: without a name, it stands in for one.
    static AudioEndpoint? ValidOrNull(AudioEndpoint? endpoint) => endpoint switch
    {
        { Id: not { Length: > 0 } } or null => null,
        { Name: null } => endpoint with { Name = endpoint.Id },
        _ => endpoint,
    };
}
