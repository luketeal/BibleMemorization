using System.Text.Json;
using System.Text.Json.Serialization;

namespace BibleMemorization.Core.Bible;

/// <summary>
/// Source-generated deserialization for the Bible API, for the same trimming reason
/// as the library's own serializer.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TranslationsResponse))]
[JsonSerializable(typeof(BooksResponse))]
[JsonSerializable(typeof(ChapterResponse))]
public partial class BibleJsonContext : JsonSerializerContext
{
    public static BibleJsonContext Api { get; } = new(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    });
}
