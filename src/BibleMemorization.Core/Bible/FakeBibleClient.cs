using System.Reflection;
using System.Text.Json;

namespace BibleMemorization.Core.Bible;

/// <summary>
/// Serves a handful of chapters from JSON bundled into the assembly.
///
/// The fixtures are in the live API's exact shape and go through the same
/// deserialization and flattening as the real client, so this exercises the real
/// parsing path rather than shortcutting it. That makes the whole import flow
/// testable with no network, and gives the deployed app an offline demo mode.
/// </summary>
public sealed class FakeBibleClient : IBibleClient
{
    private const string FixturePrefix = "BibleMemorization.Core.Bible.Fixtures.";

    public bool IsLive => false;

    public Task<IReadOnlyList<BibleTranslation>> GetTranslationsAsync(CancellationToken ct = default)
    {
        var response = ReadFixture("translations.json", BibleJsonContext.Api.TranslationsResponse);
        return Task.FromResult(response?.Translations ?? []);
    }

    public Task<IReadOnlyList<BibleBook>> GetBooksAsync(string translationId, CancellationToken ct = default)
    {
        var response = ReadFixture("books.json", BibleJsonContext.Api.BooksResponse);
        return Task.FromResult(response?.Books ?? []);
    }

    public Task<IReadOnlyList<Verse>> GetChapterAsync(
        string translationId,
        string bookId,
        int chapter,
        CancellationToken ct = default)
    {
        // Only a few chapters are bundled. Anything else comes back empty, which the
        // import page reports as "not available offline" rather than as an error.
        var name = $"{translationId}-{bookId}-{chapter}.json";
        var response = ReadFixture(name, BibleJsonContext.Api.ChapterResponse);

        return Task.FromResult(ChapterFlattener.Flatten(response?.Chapter));
    }

    /// <summary>The chapters bundled with the app, for the demo picker.</summary>
    public static IReadOnlyList<(string TranslationId, string BookId, int Chapter, string Reference)> Available { get; } =
    [
        ("eng_kjv", "JHN", 3, "John 3"),
        ("eng_kjv", "PSA", 23, "Psalm 23"),
        ("eng_kjv", "1CO", 13, "1 Corinthians 13"),
    ];

    private static T? ReadFixture<T>(
        string name,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var assembly = typeof(FakeBibleClient).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(FixturePrefix + name);

        if (stream is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize(stream, typeInfo);
    }
}
