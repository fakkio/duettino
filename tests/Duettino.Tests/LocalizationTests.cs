using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Duettino.Resources;

namespace Duettino.Tests;

public sealed partial class LocalizationTests
{
    static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it");

    // A fresh ResourceManager: the shared one caches the English set under every culture it fell back from.
    static Dictionary<string, string> StringsWithoutFallback(CultureInfo culture) =>
        new ResourceManager(typeof(Strings)).GetResourceSet(culture, createIfNotExists: true, tryParents: false)?
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!) ?? [];

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    static string[] Placeholders(string text) => [.. PlaceholderPattern().Matches(text).Select(m => m.Value).Order()];

    [Fact]
    public void Every_UI_string_has_an_Italian_version_with_the_same_placeholders()
    {
        var english = StringsWithoutFallback(CultureInfo.InvariantCulture);
        var italian = StringsWithoutFallback(Italian);

        Assert.NotEmpty(english);
        Assert.Equal(english.Keys.Order(), italian.Keys.Order());
        foreach (var (key, text) in english)
            Assert.True(Placeholders(text).SequenceEqual(Placeholders(italian[key])), $"{key}: placeholders differ");
    }

    [Theory]
    [InlineData("it-IT")]
    [InlineData("it-CH")]
    [InlineData("it")]
    public void An_Italian_display_language_gets_the_Italian_window(string displayLanguage)
    {
        Assert.Equal("● Registra", Strings.ResourceManager.GetString(nameof(Strings.RecordButton), CultureInfo.GetCultureInfo(displayLanguage)));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    public void Any_other_display_language_gets_the_English_window(string displayLanguage)
    {
        Assert.Equal("● Record", Strings.ResourceManager.GetString(nameof(Strings.RecordButton), CultureInfo.GetCultureInfo(displayLanguage)));
    }
}
