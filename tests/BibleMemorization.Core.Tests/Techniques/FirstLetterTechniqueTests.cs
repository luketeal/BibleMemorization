using BibleMemorization.Core.Techniques;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Techniques;

public class FirstLetterTechniqueTests
{
    private readonly FirstLetterTechnique _technique = new();

    [Fact]
    public void Every_word_collapses_to_its_first_letter()
    {
        var passage = Tokenizer.Tokenize("For God so loved");

        var rendered = _technique.Render(passage, new TechniqueState());

        Assert.Equal(["F", "G", "s", "l"], rendered.Where(t => t.IsWord).Select(t => t.Text));
    }

    [Fact]
    public void Original_casing_of_the_initial_is_kept()
    {
        var passage = Tokenizer.Tokenize("The LORD is my shepherd");

        var rendered = _technique.Render(passage, new TechniqueState());

        Assert.Equal(["T", "L", "i", "m", "s"], rendered.Where(t => t.IsWord).Select(t => t.Text));
    }

    [Fact]
    public void Punctuation_and_verse_numbers_are_left_alone()
    {
        var passage = Tokenizer.Tokenize("16 For God, so loved.");

        var rendered = _technique.Render(passage, new TechniqueState());

        var verseNumber = rendered[0];
        Assert.False(verseNumber.IsBlank);
        Assert.Equal("16", verseNumber.Text);

        var god = rendered.Single(t => t.TokenIndex == passage.Single(p => p.Word == "God").Index);
        Assert.Equal("G", god.Text);
        Assert.Equal(",", god.Suffix);
    }

    /// <summary>
    /// The gentlest stage, and the shape the technique had before stages existed —
    /// so progress saved against the old behaviour still renders identically.
    /// </summary>
    [Fact]
    public void An_empty_selection_shows_every_initial()
    {
        var passage = Tokenizer.Tokenize("For God so loved");

        var rendered = _technique.Render(passage, new TechniqueState());

        Assert.All(rendered.Where(t => t.IsWord), t => Assert.Single(t.Text));
        Assert.DoesNotContain(rendered, t => t.IsWord && t.Text.Length == 0);
    }

    [Fact]
    public void A_dropped_word_loses_its_initial_too()
    {
        var passage = Tokenizer.Tokenize("For God so loved");

        var rendered = _technique.Render(passage, new TechniqueState([1]));

        var dropped = rendered.Single(t => t.TokenIndex == 1);
        Assert.True(dropped.IsBlank);
        Assert.Equal(string.Empty, dropped.Text);

        // The others keep theirs: the ladder is per word, not all-or-nothing.
        Assert.Equal("s", rendered.Single(t => t.TokenIndex == 2).Text);
    }

    [Fact]
    public void A_dropped_word_still_carries_the_full_words_width()
    {
        var passage = Tokenizer.Tokenize("For God so loved");

        var rendered = _technique.Render(passage, new TechniqueState([3]));

        Assert.Equal("loved".Length, rendered.Single(t => t.TokenIndex == 3).BlankWidth);
    }

    [Fact]
    public void Dropping_every_initial_leaves_nothing_showing()
    {
        var passage = Tokenizer.Tokenize("For God so loved");

        var rendered = _technique.Render(passage, new TechniqueState(passage.WordIndices));

        Assert.All(rendered.Where(t => t.IsWord), t => Assert.Equal(string.Empty, t.Text));
    }

    [Fact]
    public void Read_along_is_not_offered_because_no_word_is_ever_fully_shown()
    {
        Assert.False(_technique.SupportsReadAlong);
    }

    [Fact]
    public void The_technique_names_its_own_controls()
    {
        Assert.Contains("initial", _technique.Vocabulary.HideAll, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("initial", _technique.Vocabulary.RevealAll, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_word_is_tested()
    {
        var passage = Tokenizer.Tokenize("16 For God so loved");

        var tested = _technique.GetTestedTokenIndices(passage, new TechniqueState());

        Assert.Equal(passage.WordIndices, tested);
    }

    [Fact]
    public void The_technique_supports_manual_selection()
    {
        // Tapping an initial drops it, which is how the stages are driven.
        Assert.True(_technique.SupportsManualWordSelection);
    }
}
