using BibleMemorization.Core.ReadAlong;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.ReadAlong;

public class ReadAlongPlannerTests
{
    private const string Verse = "For God so loved the world";

    private static TokenizedPassage Passage(string text = Verse) => Tokenizer.Tokenize(text);

    [Fact]
    public void With_nothing_hidden_the_app_reads_the_whole_passage()
    {
        var plan = ReadAlongPlanner.Plan(Passage(), []);

        var step = Assert.IsType<SpeakStep>(Assert.Single(plan));
        Assert.Equal(Verse, step.Text);
    }

    [Fact]
    public void With_everything_hidden_the_user_says_the_whole_passage()
    {
        var passage = Passage();

        var plan = ReadAlongPlanner.Plan(passage, passage.WordIndices.ToHashSet());

        var step = Assert.IsType<ListenStep>(Assert.Single(plan));
        Assert.Equal(["For", "God", "so", "loved", "the", "world"], step.ExpectedWords);
    }

    [Fact]
    public void A_hidden_word_splits_the_reading_around_it()
    {
        var passage = Passage();

        // Hide "God".
        var plan = ReadAlongPlanner.Plan(passage, [1]);

        Assert.Collection(plan,
            first => Assert.Equal("For", Assert.IsType<SpeakStep>(first).Text),
            second => Assert.Equal(["God"], Assert.IsType<ListenStep>(second).ExpectedWords),
            third => Assert.Equal("so loved the world", Assert.IsType<SpeakStep>(third).Text));
    }

    /// <summary>
    /// Two hidden words in a row are one prompt, not two. Stopping between
    /// "everlasting" and "life" would break the phrase into questions nobody asks
    /// separately.
    /// </summary>
    [Fact]
    public void Consecutive_hidden_words_become_a_single_prompt()
    {
        var passage = Passage();

        // Hide "the world".
        var plan = ReadAlongPlanner.Plan(passage, [4, 5]);

        Assert.Collection(plan,
            first => Assert.Equal("For God so loved", Assert.IsType<SpeakStep>(first).Text),
            second => Assert.Equal(["the", "world"], Assert.IsType<ListenStep>(second).ExpectedWords));
    }

    [Fact]
    public void Consecutive_visible_words_become_a_single_phrase()
    {
        var passage = Passage();

        var plan = ReadAlongPlanner.Plan(passage, [0]);

        Assert.Equal(2, plan.Count);
        Assert.Equal("God so loved the world", Assert.IsType<SpeakStep>(plan[1]).Text);
    }

    [Fact]
    public void Spoken_phrases_keep_their_punctuation_so_they_are_read_naturally()
    {
        var passage = Passage("For God so loved the world, that he gave his Son.");

        var plan = ReadAlongPlanner.Plan(passage, [1]);

        var tail = Assert.IsType<SpeakStep>(plan[2]);
        Assert.Equal("so loved the world, that he gave his Son.", tail.Text);
    }

    [Fact]
    public void Steps_carry_the_token_indices_they_cover()
    {
        var passage = Passage();

        var plan = ReadAlongPlanner.Plan(passage, [1]);

        Assert.Equal([0], plan[0].TokenIndices);
        Assert.Equal([1], plan[1].TokenIndices);
        Assert.Equal([2, 3, 4, 5], plan[2].TokenIndices);
    }

    [Fact]
    public void Verse_numbers_are_read_aloud_rather_than_asked_for()
    {
        var passage = Passage("16 For God so loved");

        var plan = ReadAlongPlanner.Plan(passage, [1]); // "For"

        // The verse number cannot be hidden, so it stays in the spoken run.
        Assert.StartsWith("16", Assert.IsType<SpeakStep>(plan[0]).Text);
    }

    [Fact]
    public void An_empty_passage_produces_no_steps()
    {
        Assert.Empty(ReadAlongPlanner.Plan(Tokenizer.Tokenize(""), []));
    }

    [Fact]
    public void The_plan_covers_every_hidden_word_exactly_once()
    {
        var passage = Passage("For God so loved the world, that he gave his only begotten Son");
        var hidden = new HashSet<int> { 1, 5, 9 };

        var plan = ReadAlongPlanner.Plan(passage, hidden);

        var asked = plan.OfType<ListenStep>().SelectMany(s => s.TokenIndices).ToArray();
        Assert.Equal(hidden.OrderBy(i => i), asked.OrderBy(i => i));
    }
}
