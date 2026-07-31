using BibleMemorization.Core.Techniques;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Techniques;

public class VanishingTextTechniqueTests
{
    private const string Verse = "For God so loved the world.";

    private readonly VanishingTextTechnique _technique = new();

    private static TokenizedPassage Passage(string text = Verse) => Tokenizer.Tokenize(text);

    [Fact]
    public void With_nothing_hidden_every_word_is_visible()
    {
        var passage = Passage();

        var rendered = _technique.Render(passage, new TechniqueState());

        Assert.All(rendered, t => Assert.False(t.IsBlank));
        Assert.Equal(Verse, string.Concat(rendered.Select(t => t.Leading + t.Prefix + t.Text + t.Suffix)));
    }

    [Fact]
    public void A_hidden_word_becomes_a_blank_carrying_its_width()
    {
        var passage = Passage();
        var state = new TechniqueState([1]); // "God"

        var rendered = _technique.Render(passage, state);

        var blank = rendered.Single(t => t.IsBlank);
        Assert.Equal(1, blank.TokenIndex);
        Assert.Equal(string.Empty, blank.Text);
        Assert.Equal(3, blank.BlankWidth);
    }

    [Fact]
    public void Punctuation_around_a_hidden_word_stays_visible()
    {
        var passage = Passage();
        var worldIndex = passage.Single(t => t.Word == "world").Index;

        var rendered = _technique.Render(passage, new TechniqueState([worldIndex]));

        var blank = rendered.Single(t => t.IsBlank);
        Assert.True(blank.IsBlank);
        Assert.Equal(".", blank.Suffix);
    }

    [Fact]
    public void Non_word_tokens_are_never_blanked_even_if_marked_hidden()
    {
        var passage = Passage("16 For God");
        // Index 0 is the verse number. Asking to hide it must be ignored.
        var rendered = _technique.Render(passage, new TechniqueState([0]));

        Assert.All(rendered, t => Assert.False(t.IsBlank));
    }

    [Fact]
    public void Only_hidden_words_are_reported_as_tested()
    {
        var passage = Passage();

        var tested = _technique.GetTestedTokenIndices(passage, new TechniqueState([1, 5]));

        Assert.Equal([1, 5], tested);
    }

    [Fact]
    public void Tested_indices_are_empty_when_nothing_is_hidden()
    {
        Assert.Empty(_technique.GetTestedTokenIndices(Passage(), new TechniqueState()));
    }

    [Fact]
    public void The_technique_supports_manual_selection()
    {
        Assert.True(_technique.SupportsManualWordSelection);
    }

    [Fact]
    public void Read_along_is_offered_because_the_unhidden_words_can_be_read()
    {
        Assert.True(_technique.SupportsReadAlong);
    }
}
