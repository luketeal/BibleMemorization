using System.Text.Json.Serialization;

namespace BibleMemorization.Core.Bible;

// Shapes mirror the Free Use Bible API (bible.helloao.org), which serves static
// JSON with no key and no rate limit.

public sealed record BibleTranslation
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("englishName")]
    public string EnglishName { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;

    [JsonPropertyName("textDirection")]
    public string TextDirection { get; init; } = "ltr";

    /// <summary>Best name to show a user, preferring the English one when present.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(EnglishName) ? Name : EnglishName;
}

public sealed record TranslationsResponse
{
    [JsonPropertyName("translations")]
    public IReadOnlyList<BibleTranslation> Translations { get; init; } = [];
}

public sealed record BibleBook
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("commonName")]
    public string CommonName { get; init; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("numberOfChapters")]
    public int NumberOfChapters { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(CommonName) ? Name : CommonName;
}

public sealed record BooksResponse
{
    [JsonPropertyName("books")]
    public IReadOnlyList<BibleBook> Books { get; init; } = [];
}

public sealed record ChapterResponse
{
    [JsonPropertyName("chapter")]
    public ChapterBody? Chapter { get; init; }
}

public sealed record ChapterBody
{
    [JsonPropertyName("number")]
    public int Number { get; init; }

    /// <summary>
    /// A heterogeneous array: verses, headings and line breaks interleaved. Parsed
    /// by hand in <see cref="ChapterFlattener"/> rather than mapped to a type.
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<System.Text.Json.JsonElement> Content { get; init; } = [];
}

/// <summary>One verse's plain text, after flattening.</summary>
public sealed record Verse(int Number, string Text);
