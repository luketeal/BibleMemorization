namespace BibleMemorization.Core.ReadAlong;

/// <summary>One stage of a guided read-along.</summary>
public abstract record ReadAlongStep
{
    /// <summary>Token indices this step covers, in passage order.</summary>
    public required IReadOnlyList<int> TokenIndices { get; init; }
}

/// <summary>Words the app reads aloud, because they are still showing.</summary>
public sealed record SpeakStep : ReadAlongStep
{
    public required string Text { get; init; }
}

/// <summary>
/// Words the user has to supply. A run of consecutive hidden words becomes one
/// prompt rather than several, so the app does not stop between "everlasting" and
/// "life" as if they were separate questions.
/// </summary>
public sealed record ListenStep : ReadAlongStep
{
    /// <summary>The words expected, for scoring and for revealing on a miss.</summary>
    public required IReadOnlyList<string> ExpectedWords { get; init; }

    public string ExpectedText => string.Join(' ', ExpectedWords);
}
