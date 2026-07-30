using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Text;

public class RecallScorerTests
{
    private const string Verse = "For God so loved the world";

    private static TokenizedPassage Passage(string text = Verse) => Tokenizer.Tokenize(text);

    [Fact]
    public void A_perfect_recitation_scores_full_marks()
    {
        var result = RecallScorer.Score(Passage(), Verse);

        Assert.True(result.IsPerfect);
        Assert.Equal(1d, result.Accuracy);
        Assert.All(result.Tokens, t => Assert.Equal(RecallOutcome.Correct, t.Outcome));
    }

    [Fact]
    public void Case_and_punctuation_do_not_affect_the_score()
    {
        var result = RecallScorer.Score(Passage(), "for god so loved the world");

        Assert.True(result.IsPerfect);
    }

    [Fact]
    public void A_substituted_word_is_marked_wrong_not_missing()
    {
        var result = RecallScorer.Score(Passage(), "For God so loved the word");

        var last = result.Tokens[^1];
        Assert.Equal(RecallOutcome.Wrong, last.Outcome);
        Assert.Equal("world", last.Expected);
        Assert.Equal("word", last.Attempted);
        Assert.Equal(5, result.CorrectCount);
    }

    [Fact]
    public void A_skipped_word_is_marked_missing()
    {
        var result = RecallScorer.Score(Passage(), "For God loved the world");

        var so = result.Tokens.Single(t => t.Expected == "so");
        Assert.Equal(RecallOutcome.Missing, so.Outcome);
        Assert.Null(so.Attempted);
    }

    /// <summary>
    /// The whole reason for edit-distance alignment: a word dropped near the start
    /// must not cascade into every later word being scored wrong.
    /// </summary>
    [Fact]
    public void Dropping_an_early_word_does_not_invalidate_the_rest()
    {
        var result = RecallScorer.Score(Passage(), "For so loved the world");

        Assert.Equal(RecallOutcome.Missing, result.Tokens.Single(t => t.Expected == "God").Outcome);

        var rest = result.Tokens.Where(t => t.Expected != "God");
        Assert.All(rest, t => Assert.Equal(RecallOutcome.Correct, t.Outcome));
    }

    [Fact]
    public void An_inserted_word_is_reported_as_extra_and_the_rest_still_aligns()
    {
        var result = RecallScorer.Score(Passage(), "For God so very much loved the world");

        Assert.Equal(["very", "much"], result.ExtraWords);
        Assert.All(result.Tokens, t => Assert.Equal(RecallOutcome.Correct, t.Outcome));
        Assert.False(result.IsPerfect);
    }

    [Fact]
    public void An_empty_attempt_marks_everything_missing()
    {
        var result = RecallScorer.Score(Passage(), "");

        Assert.Equal(0d, result.Accuracy);
        Assert.All(result.Tokens, t => Assert.Equal(RecallOutcome.Missing, t.Outcome));
    }

    [Fact]
    public void Verse_numbers_are_excluded_from_scoring()
    {
        var passage = Passage("16 For God so loved");
        var result = RecallScorer.Score(passage, "For God so loved");

        Assert.True(result.IsPerfect);
        Assert.DoesNotContain(result.Tokens, t => t.Expected == "16");
    }

    [Fact]
    public void Only_the_requested_words_are_scored()
    {
        var passage = Passage();
        var hidden = new[] { 1, 5 }; // "God" and "world"

        var result = RecallScorer.Score(passage, hidden, "God world");

        Assert.Equal(2, result.ExpectedCount);
        Assert.True(result.IsPerfect);
        Assert.Equal(["God", "world"], result.Tokens.Select(t => t.Expected));
    }

    [Fact]
    public void Accuracy_is_the_share_of_words_recalled()
    {
        var result = RecallScorer.Score(Passage(), "For God so loved the moon");

        Assert.Equal(5d / 6d, result.Accuracy, precision: 10);
    }

    [Fact]
    public void Single_word_scoring_matches_the_normalizer_rules()
    {
        var passage = Passage();

        Assert.True(RecallScorer.ScoreSingleWord(passage, 1, "god"));
        Assert.True(RecallScorer.ScoreSingleWord(passage, 1, "GOD."));
        Assert.False(RecallScorer.ScoreSingleWord(passage, 1, "good"));
        Assert.False(RecallScorer.ScoreSingleWord(passage, 1, ""));
    }

    [Fact]
    public void Scoring_a_non_word_token_is_always_false()
    {
        var passage = Passage("16 For God");

        Assert.False(RecallScorer.ScoreSingleWord(passage, 0, "16"));
        Assert.False(RecallScorer.ScoreSingleWord(passage, 99, "anything"));
    }
}
