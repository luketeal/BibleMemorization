namespace BibleMemorization.Core.Model;

/// <summary>Where a passage's text came from.</summary>
public enum PassageSource
{
    /// <summary>Imported from the Bible API.</summary>
    BibleApi,

    /// <summary>Typed by the user.</summary>
    Written,

    /// <summary>Dictated by the user.</summary>
    Dictated,
}

/// <summary>A block of text the user is memorizing.</summary>
public sealed record Passage
{
    public required Guid Id { get; init; }

    /// <summary>Shown in the library, e.g. "John 3:16-18" or a title the user chose.</summary>
    public required string Title { get; init; }

    /// <summary>Scripture reference when there is one; empty for the user's own text.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Translation id, e.g. "eng_kjv". Empty for the user's own text.</summary>
    public string Translation { get; init; } = string.Empty;

    /// <summary>Human-readable translation name, kept for attribution.</summary>
    public string TranslationName { get; init; } = string.Empty;

    public required string Text { get; init; }

    public PassageSource Source { get; init; } = PassageSource.Written;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset ModifiedUtc { get; init; }
}
