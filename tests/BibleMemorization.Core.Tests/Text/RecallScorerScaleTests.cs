using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Text;

/// <summary>
/// The alignment table is two-dimensional, so its cost grows with the product of the
/// passage length and the attempt length: a thousand words against a thousand words
/// is four megabytes in a single-threaded WASM heap, and a chapter recited in full is
/// far worse. Long inputs switch to a diagonal band instead.
///
/// A band is only safe if it is exact, so these tests are about equivalence rather
/// than speed: the banded path must produce the identical edit script the full table
/// would have, or the app starts quietly misreporting which words were missed.
/// </summary>
public class RecallScorerScaleTests
{
    /// <summary>Matches the threshold in RecallScorer, so these inputs take the banded path.</summary>
    private const int Long = 600;

    private static string Words(int count, Func<int, string>? word = null) =>
        string.Join(' ', Enumerable.Range(0, count).Select(word ?? (i => $"word{i}")));

    [Fact]
    public void A_long_perfect_recitation_scores_full_marks()
    {
        var text = Words(Long);

        var result = RecallScorer.Score(Tokenizer.Tokenize(text), text);

        Assert.True(result.IsPerfect);
        Assert.Equal(Long, result.CorrectCount);
    }

    [Fact]
    public void A_word_dropped_early_in_a_long_passage_does_not_shift_everything_after_it()
    {
        var expected = Words(Long);
        var attempt = string.Join(' ', Enumerable.Range(0, Long).Where(i => i != 3).Select(i => $"word{i}"));

        var result = RecallScorer.Score(Tokenizer.Tokenize(expected), attempt);

        // The whole reason alignment exists: one omission costs one word, not the rest.
        var missing = Assert.Single(result.Tokens, t => t.Outcome == RecallOutcome.Missing);
        Assert.Equal("word3", missing.Expected);
        Assert.Equal(Long - 1, result.CorrectCount);
    }

    [Fact]
    public void An_inserted_word_in_a_long_passage_is_reported_as_an_extra()
    {
        var expected = Words(Long);
        var attempt = expected.Replace("word3 ", "word3 interjection ");

        var result = RecallScorer.Score(Tokenizer.Tokenize(expected), attempt);

        Assert.Equal(["interjection"], result.ExtraWords);
        Assert.Equal(Long, result.CorrectCount);
    }

    [Fact]
    public void A_substitution_deep_in_a_long_passage_is_still_a_substitution()
    {
        var expected = Words(Long);
        var attempt = expected.Replace("word500 ", "wrong ");

        var result = RecallScorer.Score(Tokenizer.Tokenize(expected), attempt);

        var token = Assert.Single(result.Tokens, t => t.Expected == "word500");
        Assert.Equal(RecallOutcome.Wrong, token.Outcome);
        Assert.Equal("wrong", token.Attempted);
        Assert.Empty(result.ExtraWords);
    }

    /// <summary>
    /// The band starts narrow and widens only when it demonstrably bound. This attempt
    /// diverges far further than the starting band, so it exercises the widening —
    /// without which the result would be an approximation dressed up as a score.
    /// </summary>
    [Fact]
    public void An_attempt_diverging_further_than_the_starting_band_is_still_exact()
    {
        var expected = Words(Long);

        // A hundred words skipped in one go: far outside a 32-wide band.
        var attempt = string.Join(' ', Enumerable.Range(0, Long).Where(i => i is < 100 or >= 200).Select(i => $"word{i}"));

        var result = RecallScorer.Score(Tokenizer.Tokenize(expected), attempt);

        Assert.Equal(100, result.Tokens.Count(t => t.Outcome == RecallOutcome.Missing));
        Assert.Equal(Long - 100, result.CorrectCount);
        Assert.Empty(result.ExtraWords);
    }

    [Fact]
    public void Giving_up_after_a_few_words_of_a_long_passage_marks_the_rest_missing()
    {
        var result = RecallScorer.Score(Tokenizer.Tokenize(Words(Long)), "word0 word1 word2");

        Assert.Equal(3, result.CorrectCount);
        Assert.Equal(Long - 3, result.Tokens.Count(t => t.Outcome == RecallOutcome.Missing));
    }

    [Fact]
    public void An_attempt_bearing_no_relation_to_the_passage_still_terminates()
    {
        var result = RecallScorer.Score(Tokenizer.Tokenize(Words(Long)), Words(Long, i => $"other{i}"));

        // The band widens all the way to the full table here; the point is that it
        // finishes and reports every word rather than looping.
        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(Long, result.Tokens.Count);
    }

    /// <summary>
    /// The strongest form of the claim, run over pseudo-random edits so it is not one
    /// hand-picked case: the same edit pattern is applied at a length that takes the
    /// full table and a length that takes the band, and both must reproduce the edit
    /// script exactly. Any divergence is the band approximating, which is precisely
    /// what must not happen.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(1234)]
    [InlineData(99999)]
    public void The_banded_path_reproduces_the_edits_it_was_given(int seed)
    {
        foreach (var length in new[] { 40, Long })
        {
            var edited = Edit(length, seed);

            var result = RecallScorer.Score(Tokenizer.Tokenize(edited.ExpectedText), edited.AttemptText);

            Assert.Equal(edited.Outcomes, result.Tokens.Select(t => t.Outcome));
            Assert.Equal(edited.Extras, result.ExtraWords);
        }
    }

    private sealed record EditedAttempt(
        string ExpectedText,
        string AttemptText,
        IReadOnlyList<RecallOutcome> Outcomes,
        IReadOnlyList<string> Extras);

    /// <summary>
    /// Builds an attempt whose optimal alignment is the one that produced it, so the
    /// expected script is genuinely known rather than assumed.
    ///
    /// Two things make that true. Every word is unique, so no alternative pairing is
    /// as cheap. And edits never touch, because a deletion next to an insertion costs
    /// the same as one substitution — a tie the scorer could legitimately break either
    /// way, which would make this a test of tie-breaking rather than of correctness.
    /// </summary>
    private static EditedAttempt Edit(int length, int seed)
    {
        const int spacing = 3;

        var random = new Random(seed);
        var expected = Enumerable.Range(0, length).Select(i => $"w{i}").ToArray();

        var attempt = new List<string>();
        var outcomes = new List<RecallOutcome>();
        var extras = new List<string>();
        var cooldown = 0;

        for (var i = 0; i < length; i++)
        {
            var edit = cooldown > 0 ? -1 : random.Next(9);

            if (cooldown > 0)
            {
                cooldown--;
            }
            else if (edit < 3)
            {
                cooldown = spacing;
            }

            switch (edit)
            {
                case 0:
                    // Skipped entirely.
                    outcomes.Add(RecallOutcome.Missing);
                    break;

                case 1:
                    // An interjection before the right word.
                    attempt.Add($"extra{i}");
                    extras.Add($"extra{i}");
                    attempt.Add(expected[i]);
                    outcomes.Add(RecallOutcome.Correct);
                    break;

                case 2:
                    // The wrong word in the right place.
                    attempt.Add($"wrong{i}");
                    outcomes.Add(RecallOutcome.Wrong);
                    break;

                default:
                    attempt.Add(expected[i]);
                    outcomes.Add(RecallOutcome.Correct);
                    break;
            }
        }

        return new EditedAttempt(
            string.Join(' ', expected),
            string.Join(' ', attempt),
            outcomes,
            extras);
    }
}
