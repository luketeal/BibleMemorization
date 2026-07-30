using BibleMemorization.Core.Techniques;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Techniques;

public class WordHiderTests
{
    private const string Verse =
        "For God so loved the world, that he gave his only begotten Son, "
        + "that whosoever believeth in him should not perish, but have everlasting life.";

    private static TokenizedPassage Passage() => Tokenizer.Tokenize(Verse);

    [Fact]
    public void Hide_all_hides_every_word_but_no_punctuation()
    {
        var passage = Passage();

        var hidden = WordHider.HideAll(passage);

        Assert.Equal(passage.WordIndices, hidden);
        Assert.All(hidden, i => Assert.True(passage[i].IsWord));
    }

    [Fact]
    public void Reveal_all_clears_the_selection()
    {
        Assert.Empty(WordHider.RevealAll());
    }

    [Fact]
    public void Hiding_more_adds_exactly_the_requested_number()
    {
        var passage = Passage();

        var hidden = WordHider.HideMore(passage, [], count: 5, seed: 1);

        Assert.Equal(5, hidden.Count);
    }

    [Fact]
    public void Hiding_more_keeps_what_was_already_hidden()
    {
        var passage = Passage();
        var first = WordHider.HideMore(passage, [], count: 3, seed: 1);

        var second = WordHider.HideMore(passage, first, count: 4, seed: 2);

        Assert.Equal(7, second.Count);
        Assert.All(first, i => Assert.Contains(i, second));
    }

    [Fact]
    public void The_same_seed_hides_the_same_words()
    {
        var passage = Passage();

        var a = WordHider.HideMore(passage, [], count: 6, seed: 42);
        var b = WordHider.HideMore(passage, [], count: 6, seed: 42);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Asking_for_more_words_than_remain_hides_the_rest_without_failing()
    {
        var passage = Passage();

        var hidden = WordHider.HideMore(passage, [], count: 10_000, seed: 1);

        Assert.Equal(passage.WordCount, hidden.Count);
    }

    [Fact]
    public void Hiding_a_percentage_always_hides_at_least_one_word()
    {
        var passage = Passage();

        // A percentage this small rounds towards nothing; the button must still act.
        var hidden = WordHider.HideMorePercent(passage, [], percent: 0.0001, seed: 1);

        Assert.Single(hidden);
    }

    [Fact]
    public void Hiding_ten_percent_rounds_up()
    {
        var passage = Passage();

        var hidden = WordHider.HideMorePercent(passage, [], percent: 0.1, seed: 1);

        Assert.Equal((int)Math.Ceiling(passage.WordCount * 0.1), hidden.Count);
    }

    [Fact]
    public void Toggling_hides_then_reveals_the_same_word()
    {
        var passage = Passage();
        var index = passage.WordIndices[3];

        var hidden = WordHider.Toggle(passage, [], index);
        Assert.Contains(index, hidden);

        var revealed = WordHider.Toggle(passage, hidden, index);
        Assert.DoesNotContain(index, revealed);
    }

    [Fact]
    public void Toggling_a_non_word_token_does_nothing()
    {
        var passage = Tokenizer.Tokenize("16 For God");

        var hidden = WordHider.Toggle(passage, [], tokenIndex: 0);

        Assert.Empty(hidden);
    }

    [Fact]
    public void Toggling_an_out_of_range_index_does_nothing()
    {
        var passage = Passage();

        Assert.Empty(WordHider.Toggle(passage, [], tokenIndex: -1));
        Assert.Empty(WordHider.Toggle(passage, [], tokenIndex: 9_999));
    }

    [Fact]
    public void Results_are_always_sorted_so_state_compares_predictably()
    {
        var passage = Passage();

        var hidden = WordHider.HideMore(passage, [], count: 8, seed: 7);

        Assert.Equal(hidden.OrderBy(i => i), hidden);
    }
}
