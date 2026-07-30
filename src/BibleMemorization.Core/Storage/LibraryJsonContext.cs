using System.Text.Json;
using System.Text.Json.Serialization;
using BibleMemorization.Core.Model;

namespace BibleMemorization.Core.Storage;

/// <summary>
/// Source-generated serialization for everything that gets persisted.
///
/// This is not a style preference. Blazor WebAssembly Release builds trim unused
/// code, and reflection-based System.Text.Json breaks under trimming in a
/// particularly nasty way: it works in Debug and fails only in the deployed app.
/// Source generation makes the serializers statically reachable so trimming cannot
/// remove them.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(LibrarySnapshot))]
[JsonSerializable(typeof(Passage))]
[JsonSerializable(typeof(PracticeProgress))]
[JsonSerializable(typeof(Attempt))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(IReadOnlyList<Passage>))]
[JsonSerializable(typeof(IReadOnlyList<PracticeProgress>))]
public partial class LibraryJsonContext : JsonSerializerContext
{
    /// <summary>Indented, for the .save file a user might actually open and read.</summary>
    public static LibraryJsonContext Pretty { get; } = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    });

    /// <summary>Compact, for localStorage where the 5MB budget is worth respecting.</summary>
    public static LibraryJsonContext Compact { get; } = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    });
}
