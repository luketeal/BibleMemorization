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
    /// Scores answers typed into specific blanks.
    ///
    /// Deliberately positional rather than aligned. Alignment exists to absorb words
    /// shifting, which cannot happen here: each answer was typed into a known gap.
    /// Running alignment over it actively misleads — a wrong answer can pair with a
    /// later blank and get reported against a word the user never typed into.
    /// </summary>
    /// <param name="prefilled">
    /// What each field started with, when a technique seeds them — first-letter puts
    /// the initial in the box. A field left at its seed was not answered, and saying
    /// "wrong word" about a word the user never typed would misreport it.
    /// </param>
    public static RecallResult ScoreBlanks(
        TokenizedPassage passage,
        IReadOnlyList<int> expectedTokenIndices,
        IReadOnlyDictionary<int, string> answers,
        IReadOnlyDictionary<int, string>? prefilled = null)
    {
        var results = new List<TokenRecall>();

        foreach (var index in expectedTokenIndices.Where(i => i >= 0 && i < passage.Count && passage[i].IsWord)
                                                  .OrderBy(i => i))
        {
            var expected = passage[index].Word;
            answers.TryGetValue(index, out var answer);

            if (string.IsNullOrWhiteSpace(answer))
            {
                results.Add(new TokenRecall(index, expected, null, RecallOutcome.Missing));
                continue;
            }

            // Matching is checked before "untouched" on purpose. For a one-letter word
            // the seed already is the whole answer, so leaving it alone is correct
            // rather than unanswered.
            if (TextNormalizer.WordsMatch(expected, answer))
            {
                results.Add(new TokenRecall(index, expected, answer.Trim(), RecallOutcome.Correct));
                continue;
            }

            if (prefilled is not null
                && prefilled.TryGetValue(index, out var seed)
                && seed.Length > 0
                && string.Equals(answer.Trim(), seed, StringComparison.Ordinal))
            {
                results.Add(new TokenRecall(index, expected, null, RecallOutcome.Missing));
                continue;
            }

            results.Add(new TokenRecall(index, expected, answer.Trim(), RecallOutcome.Wrong));
        }

        return new RecallResult(results, []);
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
    /// Above this many cells the full table stops being a reasonable thing to hand a
    /// single-threaded WASM heap: 250,000 ints is about 1 MB, and the table grows with
    /// the product of both lengths, so a chapter-length passage recited in full would
    /// ask for tens of megabytes.
    /// </summary>
    private const int FullTableCellLimit = 250_000;

    /// <summary>
    /// Levenshtein alignment over word sequences, returning the edit script rather
    /// than just the distance. Substitution is what distinguishes "said the wrong
    /// word here" from "skipped a word", which the UI colours differently.
    ///
    /// The backtrace needs the table, so the cost rows cannot simply roll. Instead,
    /// long inputs switch to a diagonal band. Any optimal path strays from the
    /// diagonal by at most the edit distance, so a band at least that wide contains
    /// the very path the full table would have walked — same script, a fraction of
    /// the memory. The band starts narrow and doubles until it demonstrably did not
    /// bind, which is what keeps the result exact rather than approximate.
    /// </summary>
    private static List<Op> Align(string[] expected, string[] actual)
    {
        var n = expected.Length;
        var m = actual.Length;

        if ((long)(n + 1) * (m + 1) <= FullTableCellLimit)
        {
            return AlignFull(expected, actual);
        }

        // Below |n - m| no path reaches the far corner at all, so that is the floor.
        var band = Math.Max(32, Math.Abs(n - m));
        var maxBand = Math.Max(n, m);

        while (true)
        {
            band = Math.Min(band, maxBand);
            var (ops, distance) = AlignBanded(expected, actual, band);

            // A distance within the band proves the band held every cheaper path, so
            // this is the true optimum. At maxBand the band is the whole table, and
            // the distance can never exceed max(n, m) — hence this always terminates.
            if (distance <= band)
            {
                return ops;
            }

            band *= 2;
        }
    }

    private static List<Op> AlignFull(string[] expected, string[] actual)
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

    /// <summary>
    /// The same recurrence and the same backtrace preferences as <see cref="AlignFull"/>,
    /// but only cells within <paramref name="band"/> of the diagonal are stored.
    /// Returns the distance alongside the script so the caller can tell whether the
    /// band bound — if it did, the script is not trustworthy and is not returned.
    /// </summary>
    private static (List<Op> Ops, int Distance) AlignBanded(string[] expected, string[] actual, int band)
    {
        // Half of int.MaxValue, so "unreachable + 1" cannot wrap into a small number
        // and look like a cheap path.
        const int unreachable = int.MaxValue / 2;

        var n = expected.Length;
        var m = actual.Length;

        var lo = new int[n + 1];
        var rows = new int[n + 1][];

        for (var i = 0; i <= n; i++)
        {
            lo[i] = Math.Max(0, i - band);
            var rowEnd = Math.Min(m, i + band);
            rows[i] = new int[rowEnd - lo[i] + 1];
            Array.Fill(rows[i], unreachable);
        }

        int Cost(int i, int j)
        {
            if (i < 0 || j < 0 || i > n || j > m)
            {
                return unreachable;
            }

            var offset = j - lo[i];
            return offset < 0 || offset >= rows[i].Length ? unreachable : rows[i][offset];
        }

        void SetCost(int i, int j, int value)
        {
            var offset = j - lo[i];
            if (offset >= 0 && offset < rows[i].Length)
            {
                rows[i][offset] = value;
            }
        }

        SetCost(0, 0, 0);

        for (var j = 1; j <= Math.Min(m, band); j++)
        {
            SetCost(0, j, j);
        }

        for (var i = 1; i <= n; i++)
        {
            if (i <= band)
            {
                SetCost(i, 0, i);
            }

            var rowEnd = Math.Min(m, i + band);

            for (var j = Math.Max(1, lo[i]); j <= rowEnd; j++)
            {
                var substitution = Cost(i - 1, j - 1) + (expected[i - 1] == actual[j - 1] ? 0 : 1);
                var deletion = Cost(i - 1, j) + 1;
                var insertion = Cost(i, j - 1) + 1;
                SetCost(i, j, Math.Min(substitution, Math.Min(deletion, insertion)));
            }
        }

        var distance = Cost(n, m);

        if (distance > band)
        {
            // The band bound. Backtracing through clipped cells would wander, so the
            // caller widens and asks again rather than being handed a wrong script.
            return ([], distance);
        }

        var ops = new List<Op>(n + m);
        var x = n;
        var y = m;

        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0)
            {
                var isMatch = expected[x - 1] == actual[y - 1];
                if (Cost(x, y) == Cost(x - 1, y - 1) + (isMatch ? 0 : 1))
                {
                    ops.Add(isMatch ? Op.Match : Op.Substitute);
                    x--;
                    y--;
                    continue;
                }
            }

            if (x > 0 && Cost(x, y) == Cost(x - 1, y) + 1)
            {
                ops.Add(Op.Missing);
                x--;
                continue;
            }

            ops.Add(Op.Extra);
            y--;
        }

        ops.Reverse();
        return (ops, distance);
    }
}
