namespace BibleMemorization.Core.Text;

/// <summary>
/// Aligns what the user produced against what the passage expects.
///
/// A positional word-by-word comparison would be useless here: drop one word early
/// on and every later word shifts, turning a near-perfect recitation into a wall of
/// red. So this runs a word-level edit-distance alignment instead, which absorbs a
/// dropped or added word and keeps the rest lined up.
///
/// This is the single scoring path for typed recitation and speech transcripts
/// alike, so both are judged by identical rules.
/// </summary>
public static class RecallScorer
{
    private enum Op
    {
        Match,
        Substitute,
        Missing,
        Extra,
    }

    /// <summary>
    /// Scores an attempt against every word of the passage.
    /// </summary>
    public static RecallResult Score(TokenizedPassage passage, string? attempt) =>
        Score(passage, passage.WordIndices, attempt);

    /// <summary>
    /// Scores an attempt against a chosen subset of the passage's words — used when
    /// only the hidden words are being tested rather than the whole passage.
    /// </summary>
    public static RecallResult Score(
        TokenizedPassage passage,
        IReadOnlyList<int> expectedTokenIndices,
        string? attempt)
    {
        var expected = expectedTokenIndices
            .Where(i => i >= 0 && i < passage.Count && passage[i].IsWord)
            .OrderBy(i => i)
            .ToArray();

        var attemptWords = SplitAttempt(attempt);

        if (expected.Length == 0)
        {
            return new RecallResult([], attemptWords.Select(w => w.Original).ToArray());
        }

        var expectedNormalized = expected.Select(i => TextNormalizer.NormalizeWord(passage[i].Word)).ToArray();
        var ops = Align(expectedNormalized, attemptWords.Select(w => w.Normalized).ToArray());

        var results = new List<TokenRecall>(expected.Length);
        var extras = new List<string>();
        var e = 0;
        var a = 0;

        foreach (var op in ops)
        {
            switch (op)
            {
                case Op.Match:
                    results.Add(new TokenRecall(
                        expected[e], passage[expected[e]].Word, attemptWords[a].Original, RecallOutcome.Correct));
                    e++;
                    a++;
                    break;

                case Op.Substitute:
                    results.Add(new TokenRecall(
                        expected[e], passage[expected[e]].Word, attemptWords[a].Original, RecallOutcome.Wrong));
                    e++;
                    a++;
                    break;

                case Op.Missing:
                    results.Add(new TokenRecall(
                        expected[e], passage[expected[e]].Word, null, RecallOutcome.Missing));
                    e++;
                    break;

                case Op.Extra:
                    extras.Add(attemptWords[a].Original);
                    a++;
                    break;
            }
        }

        return new RecallResult(results, extras);
    }

    /// <summary>
    /// Convenience for the single-blank case, where one word is typed into one gap
    /// and alignment has nothing to absorb.
    /// </summary>
    public static bool ScoreSingleWord(TokenizedPassage passage, int tokenIndex, string? attempt)
    {
        if (tokenIndex < 0 || tokenIndex >= passage.Count || !passage[tokenIndex].IsWord)
        {
            return false;
        }

        return TextNormalizer.WordsMatch(passage[tokenIndex].Word, attempt);
    }

    private static (string Original, string Normalized)[] SplitAttempt(string? attempt)
    {
        if (string.IsNullOrWhiteSpace(attempt))
        {
            return [];
        }

        return attempt
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => (Original: w, Normalized: TextNormalizer.NormalizeWord(w)))
            .Where(w => w.Normalized.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Levenshtein alignment over word sequences, returning the edit script rather
    /// than just the distance. Substitution is what distinguishes "said the wrong
    /// word here" from "skipped a word", which the UI colours differently.
    /// </summary>
    private static List<Op> Align(string[] expected, string[] actual)
    {
        var n = expected.Length;
        var m = actual.Length;
        var cost = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++)
        {
            cost[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            cost[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var substitution = cost[i - 1, j - 1] + (expected[i - 1] == actual[j - 1] ? 0 : 1);
                var deletion = cost[i - 1, j] + 1;
                var insertion = cost[i, j - 1] + 1;
                cost[i, j] = Math.Min(substitution, Math.Min(deletion, insertion));
            }
        }

        // Walk back through the table to recover the operations, then reverse.
        var ops = new List<Op>(n + m);
        var x = n;
        var y = m;

        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0)
            {
                var isMatch = expected[x - 1] == actual[y - 1];
                if (cost[x, y] == cost[x - 1, y - 1] + (isMatch ? 0 : 1))
                {
                    ops.Add(isMatch ? Op.Match : Op.Substitute);
                    x--;
                    y--;
                    continue;
                }
            }

            if (x > 0 && cost[x, y] == cost[x - 1, y] + 1)
            {
                ops.Add(Op.Missing);
                x--;
                continue;
            }

            ops.Add(Op.Extra);
            y--;
        }

        ops.Reverse();
        return ops;
    }
}
