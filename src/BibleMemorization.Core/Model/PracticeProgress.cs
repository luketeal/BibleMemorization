using System.Text.Json.Serialization;

namespace BibleMemorization.Core.Model;

/// <summary>How the user supplied their answer.</summary>
public enum InputMode
{
    Typed,
    Spoken,
}

/// <summary>One recorded attempt at recalling a passage.</summary>
public sealed record Attempt
{
    public required DateTimeOffset Utc { get; init; }

    public required string TechniqueId { get; init; }

    public InputMode InputMode { get; init; }

    /// <summary>Share of tested words recalled, 0 to 1.</summary>
    public double Accuracy { get; init; }

    public int DurationMs { get; init; }
}

/// <summary>
/// Practice state for one passage under one technique. Keyed by both, so switching
/// technique does not disturb the words hidden under the previous one.
/// </summary>
public sealed record PracticeProgress
{
    public required Guid PassageId { get; init; }

    public required string TechniqueId { get; init; }

    /// <summary>Token indices the user has hidden.</summary>
    public IReadOnlyList<int> HiddenTokenIndices { get; init; } = [];

    public IReadOnlyList<Attempt> Attempts { get; init; } = [];

    /// <summary>
    /// When this progress last changed. Absent in files written before this existed,
    /// which defaults to MinValue — so an old backup can never overwrite newer work.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; }

    public static string KeyFor(Guid passageId, string techniqueId) => $"{passageId:N}:{techniqueId}";

    // Derived, so writing it into the save file would just be noise in a file the
    // README invites people to open.
    [JsonIgnore]
    public string Key => KeyFor(PassageId, TechniqueId);
}
