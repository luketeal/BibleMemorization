namespace BibleMemorization.Core.Bible;

/// <summary>The text of a passage, with everything needed to attribute it.</summary>
public sealed record PassageText(
    string Reference,
    string TranslationId,
    string TranslationName,
    string Text);

/// <summary>
/// Reads scripture from somewhere.
///
/// Behind an interface so the app can run against bundled sample text when the API
/// is unreachable — which is also what makes the whole import flow testable offline.
/// </summary>
public interface IBibleClient
{
    /// <summary>True when this client talks to the network.</summary>
    bool IsLive { get; }

    Task<IReadOnlyList<BibleTranslation>> GetTranslationsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BibleBook>> GetBooksAsync(string translationId, CancellationToken ct = default);

    Task<IReadOnlyList<Verse>> GetChapterAsync(
        string translationId,
        string bookId,
        int chapter,
        CancellationToken ct = default);
}
