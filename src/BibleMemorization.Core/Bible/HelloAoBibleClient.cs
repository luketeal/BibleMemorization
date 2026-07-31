using System.Text.Json;

namespace BibleMemorization.Core.Bible;

/// <summary>
/// Reads from the Free Use Bible API at bible.helloao.org: static JSON on a CDN,
/// no API key, no rate limit, CORS open, so a static site can call it directly
/// from the browser with no backend of its own.
/// </summary>
public sealed class HelloAoBibleClient(HttpClient httpClient) : IBibleClient
{
    public const string DefaultBaseAddress = "https://bible.helloao.org/api/";

    public bool IsLive => true;

    public async Task<IReadOnlyList<BibleTranslation>> GetTranslationsAsync(CancellationToken ct = default)
    {
        var response = await GetAsync<TranslationsResponse>(
            "available_translations.json", BibleJsonContext.Api.TranslationsResponse, ct);

        return response?.Translations ?? [];
    }

    public async Task<IReadOnlyList<BibleBook>> GetBooksAsync(string translationId, CancellationToken ct = default)
    {
        var response = await GetAsync<BooksResponse>(
            $"{translationId}/books.json", BibleJsonContext.Api.BooksResponse, ct);

        return response?.Books ?? [];
    }

    public async Task<IReadOnlyList<Verse>> GetChapterAsync(
        string translationId,
        string bookId,
        int chapter,
        CancellationToken ct = default)
    {
        var response = await GetAsync<ChapterResponse>(
            $"{translationId}/{bookId}/{chapter}.json", BibleJsonContext.Api.ChapterResponse, ct);

        return ChapterFlattener.Flatten(response?.Chapter);
    }

    private async Task<T?> GetAsync<T>(
        string path,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        var response = await httpClient.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);
    }
}
