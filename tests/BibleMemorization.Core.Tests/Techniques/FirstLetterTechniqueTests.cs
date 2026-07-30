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

    [Fact]
    public void Hidden_state_is_ignored_because_the_technique_hides_everything()
    {
        var passage = Tokenizer.Tokenize("For God so loved");

        var withState = _technique.Render(passage, new TechniqueState([1]));
        var withoutState = _technique.Render(passage, new TechniqueState());

        Assert.Equal(withoutState, withState);
    }

    [Fact]
    public void Every_word_is_tested()
    {
        var passage = Tokenizer.Tokenize("16 For God so loved");

        var tested = _technique.GetTestedTokenIndices(passage, new TechniqueState());

        Assert.Equal(passage.WordIndices, tested);
    }

    [Fact]
    public void The_technique_does_not_support_manual_selection()
    {
        Assert.False(_technique.SupportsManualWordSelection);
    }
}
