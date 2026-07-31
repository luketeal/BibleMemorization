using System.Text.Json.Serialization;
using BibleMemorization.Core.Model;

namespace BibleMemorization.Core.Storage;

/// <summary>
/// The entire library in one object: the single serialization contract shared by
/// every storage backend. The .save file is literally this, serialized — which is
/// why browser storage and file export can be two implementations of one interface
/// rather than two separate mechanisms.
/// </summary>
public sealed record LibrarySnapshot
{
    /// <summary>
    /// Bumped when the shape changes incompatibly. A file from a newer version is
    /// rejected outright rather than partially loaded.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset ExportedUtc { get; init; }

    public IReadOnlyList<Passage> Passages { get; init; } = [];

    public IReadOnlyList<PracticeProgress> Progress { get; init; } = [];

    public AppSettings Settings { get; init; } = AppSettings.Default;

    public static LibrarySnapshot Empty { get; } = new();

    [JsonIgnore]
    public bool IsEmpty => Passages.Count == 0 && Progress.Count == 0;
}
