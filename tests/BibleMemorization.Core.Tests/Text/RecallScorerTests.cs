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

    // ---- Filling in specific blanks ----

    [Fact]
    public void Blank_answers_are_scored_against_the_blank_they_were_typed_into()
    {
        var passage = Passage();
        var tested = new[] { 1, 5 }; // "God" and "world"

        var result = RecallScorer.ScoreBlanks(passage, tested, new Dictionary<int, string>
        {
            [1] = "God",
            [5] = "earth",
        });

        Assert.Equal(RecallOutcome.Correct, result.Tokens.Single(t => t.TokenIndex == 1).Outcome);

        var wrong = result.Tokens.Single(t => t.TokenIndex == 5);
        Assert.Equal(RecallOutcome.Wrong, wrong.Outcome);
        Assert.Equal("world", wrong.Expected);
        Assert.Equal("earth", wrong.Attempted);
    }

    /// <summary>
    /// The bug this method exists to prevent. Alignment absorbs shifts, which cannot
    /// happen when each answer was typed into a known gap - so a wrong answer used
    /// to pair with a later blank and be reported against a word the user never
    /// typed into.
    /// </summary>
    [Fact]
    public void A_wrong_answer_never_slides_onto_a_different_blank()
    {
        var passage = Passage("For God so loved the world");
        var tested = new[] { 1, 5 };

        var result = RecallScorer.ScoreBlanks(passage, tested, new Dictionary<int, string>
        {
            [5] = "earth",
        });

        // "God" was left blank, so it is missing - not matched against "earth".
        var god = result.Tokens.Single(t => t.TokenIndex == 1);
        Assert.Equal(RecallOutcome.Missing, god.Outcome);
        Assert.Null(god.Attempted);

        var world = result.Tokens.Single(t => t.TokenIndex == 5);
        Assert.Equal(RecallOutcome.Wrong, world.Outcome);
        Assert.Equal("earth", world.Attempted);
    }

    [Fact]
    public void An_empty_blank_counts_as_missing()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(passage, [1], new Dictionary<int, string> { [1] = "   " });

        Assert.Equal(RecallOutcome.Missing, result.Tokens.Single().Outcome);
    }

    [Fact]
    public void Blank_answers_forgive_case_and_punctuation()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(passage, [5], new Dictionary<int, string> { [5] = "WORLD." });

        Assert.True(result.IsPerfect);
    }

    [Fact]
    public void Blank_scoring_never_reports_extra_words()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(passage, [1], new Dictionary<int, string> { [1] = "many words here" });

        // There is nowhere for an extra word to go: one gap takes one answer.
        Assert.Empty(result.ExtraWords);
        Assert.Equal(RecallOutcome.Wrong, result.Tokens.Single().Outcome);
    }

    [Fact]
    public void Blank_results_come_back_in_passage_order()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(passage, [5, 1], new Dictionary<int, string>());

        Assert.Equal([1, 5], result.Tokens.Select(t => t.TokenIndex));
    }

    // ---- Fields a technique pre-fills, e.g. first letter's initial ----

    /// <summary>
    /// The bug this guards. First letter seeds each field with the initial, so a
    /// field still holding just "G" was never answered. Calling that a wrong word
    /// would blame the user for a word they never typed.
    /// </summary>
    [Fact]
    public void A_field_left_at_its_seed_counts_as_unanswered()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(
            passage,
            [1],
            new Dictionary<int, string> { [1] = "G" },
            new Dictionary<int, string> { [1] = "G" });

        var token = result.Tokens.Single();
        Assert.Equal(RecallOutcome.Missing, token.Outcome);
        Assert.Null(token.Attempted);
    }

    [Fact]
    public void Completing_a_seeded_field_into_the_whole_word_is_correct()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(
            passage,
            [1],
            new Dictionary<int, string> { [1] = "God" },
            new Dictionary<int, string> { [1] = "G" });

        Assert.True(result.IsPerfect);
    }

    /// <summary>
    /// For a one-letter word the seed already is the answer, so touching nothing is
    /// right. This is why matching is checked before "still the seed".
    /// </summary>
    [Fact]
    public void A_one_letter_word_left_at_its_seed_is_correct()
    {
        var passage = Passage("I am he");

        var result = RecallScorer.ScoreBlanks(
            passage,
            [0],
            new Dictionary<int, string> { [0] = "I" },
            new Dictionary<int, string> { [0] = "I" });

        Assert.Equal(RecallOutcome.Correct, result.Tokens.Single().Outcome);
    }

    [Fact]
    public void Replacing_a_seed_with_the_wrong_word_is_still_wrong()
    {
        var passage = Passage();

        var result = RecallScorer.ScoreBlanks(
            passage,
            [1],
            new Dictionary<int, string> { [1] = "Good" },
            new Dictionary<int, string> { [1] = "G" });

        var token = result.Tokens.Single();
        Assert.Equal(RecallOutcome.Wrong, token.Outcome);
        Assert.Equal("Good", token.Attempted);
    }

    [Fact]
    public void Without_seeds_a_lone_letter_is_judged_on_its_merits()
    {
        var passage = Passage();

        // Vanishing text seeds nothing, so "G" here is simply a wrong answer.
        var result = RecallScorer.ScoreBlanks(passage, [1], new Dictionary<int, string> { [1] = "G" });

        Assert.Equal(RecallOutcome.Wrong, result.Tokens.Single().Outcome);
    }
}
