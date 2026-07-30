namespace BibleMemorization.Core.Text;

public enum RecallOutcome
{
    /// <summary>The user produced this word.</summary>
    Correct,

    /// <summary>The user produced a different word in this position.</summary>
    Wrong,

    /// <summary>The user skipped this word entirely.</summary>
    Missing,
}

/// <summary>How one expected word fared.</summary>
/// <param name="TokenIndex">Index into the <see cref="TokenizedPassage"/>.</param>
/// <param name="Expected">The word as written in the passage.</param>
/// <param name="Attempted">What the user said or typed here, if anything.</param>
public sealed record TokenRecall(
    int TokenIndex,
    string Expected,
    string? Attempted,
    RecallOutcome Outcome);

/// <summary>
/// The outcome of one recall attempt over a set of expected words.
/// </summary>
public sealed record RecallResult(
    IReadOnlyList<TokenRecall> Tokens,
    IReadOnlyList<string> ExtraWords)
{
    public int ExpectedCount => Tokens.Count;

    public int CorrectCount => Tokens.Count(t => t.Outcome == RecallOutcome.Correct);

    /// <summary>
    /// Share of expected words recalled, 0 to 1. Extra words are reported separately
    /// rather than subtracted, so a user who adds a stray word is not punished twice.
    /// </summary>
    public double Accuracy => ExpectedCount == 0 ? 1d : (double)CorrectCount / ExpectedCount;

    public bool IsPerfect => CorrectCount == ExpectedCount && ExtraWords.Count == 0;

    public static RecallResult Empty { get; } = new([], []);
}
