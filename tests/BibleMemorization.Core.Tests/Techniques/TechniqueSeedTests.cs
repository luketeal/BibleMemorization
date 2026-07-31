using BibleMemorization.Core.Techniques;

namespace BibleMemorization.Core.Tests.Techniques;

/// <summary>
/// WordHider is deliberately reproducible: the same seed hides the same words. The
/// seed is what carries that guarantee out of the library and into the page, so if
/// the seed drifts the reproducibility is gone whatever WordHider does.
/// </summary>
public class TechniqueSeedTests
{
    private static readonly Guid Passage = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void The_same_inputs_give_the_same_seed()
    {
        Assert.Equal(
            TechniqueSeed.For(Passage, "vanishing-text", 3),
            TechniqueSeed.For(Passage, "vanishing-text", 3));
    }

    /// <summary>
    /// The failure this exists to prevent, and the reason the seed is not built from
    /// HashCode.Combine: .NET randomizes string hashing per process, so a seed folding
    /// in the technique id that way differed on every page load. Within one process
    /// that is invisible — hence a value pinned here, which a per-process hash cannot
    /// reproduce.
    /// </summary>
    [Fact]
    public void The_seed_is_a_fixed_value_rather_than_a_per_process_hash()
    {
        Assert.Equal(237457641, TechniqueSeed.For(Passage, "vanishing-text", 3));
        Assert.Equal(-1108003980, TechniqueSeed.For(Passage, "first-letter", 0));
    }

    [Fact]
    public void Hiding_more_words_moves_the_seed_on()
    {
        // Otherwise pressing "hide 20%" twice would reshuffle the same choice.
        var seeds = Enumerable.Range(0, 10).Select(n => TechniqueSeed.For(Passage, "vanishing-text", n));

        Assert.Equal(10, seeds.Distinct().Count());
    }

    [Fact]
    public void Different_techniques_get_different_seeds()
    {
        Assert.NotEqual(
            TechniqueSeed.For(Passage, "vanishing-text", 3),
            TechniqueSeed.For(Passage, "first-letter", 3));
    }

    [Fact]
    public void Different_passages_get_different_seeds()
    {
        Assert.NotEqual(
            TechniqueSeed.For(Passage, "vanishing-text", 3),
            TechniqueSeed.For(Guid.Parse("22222222-2222-2222-2222-222222222222"), "vanishing-text", 3));
    }

    [Fact]
    public void The_same_seed_hides_the_same_words()
    {
        var passage = Core.Text.Tokenizer.Tokenize("For God so loved the world that he gave his only begotten Son");
        var seed = TechniqueSeed.For(Passage, "vanishing-text", 0);

        Assert.Equal(
            WordHider.HideMore(passage, [], 4, seed),
            WordHider.HideMore(passage, [], 4, seed));
    }
}
