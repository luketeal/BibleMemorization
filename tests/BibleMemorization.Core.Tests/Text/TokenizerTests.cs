using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Text;

public class TokenizerTests
{
    [Theory]
    [InlineData("For God so loved the world.")]
    [InlineData("  leading and trailing whitespace  ")]
    [InlineData("The LORD is my shepherd; I shall not want.")]
    [InlineData("Jesus wept.")]
    [InlineData("16 For God so loved the world, that he gave his only begotten Son")]
    [InlineData("line one\nline two\n\nline four")]
    [InlineData("\"Quoted words,\" he said -- then paused.")]
    [InlineData("God's loving-kindness endureth for ever.")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Café naïve résumé")]
    public void Tokenizing_round_trips_the_original_text_exactly(string text)
    {
        var passage = Tokenizer.Tokenize(text);

        Assert.Equal(text, passage.ToOriginalText());
    }

    [Fact]
    public void Punctuation_is_peeled_off_the_edges_of_words()
    {
        var passage = Tokenizer.Tokenize("\"Quoted,\" said he.");

        Assert.Equal(["Quoted", "said", "he"], passage.Where(t => t.IsWord).Select(t => t.Word));

        var quoted = passage[0];
        Assert.Equal("\"", quoted.Prefix);
        Assert.Equal("Quoted", quoted.Word);
        Assert.Equal(",\"", quoted.Suffix);
    }

    [Fact]
    public void Apostrophes_and_hyphens_stay_inside_words()
    {
        var passage = Tokenizer.Tokenize("God's loving-kindness");

        Assert.Equal(["God's", "loving-kindness"], passage.Where(t => t.IsWord).Select(t => t.Word));
    }

    [Fact]
    public void Verse_numbers_are_not_treated_as_words()
    {
        var passage = Tokenizer.Tokenize("16 For God so loved");

        Assert.False(passage[0].IsWord);
        Assert.Equal("16", passage[0].Word);
        Assert.Equal(["For", "God", "so", "loved"], passage.Where(t => t.IsWord).Select(t => t.Word));
    }

    [Fact]
    public void Standalone_punctuation_is_not_a_word()
    {
        var passage = Tokenizer.Tokenize("wait -- then go");

        Assert.Equal(["wait", "then", "go"], passage.Where(t => t.IsWord).Select(t => t.Word));
        Assert.Contains(passage, t => !t.IsWord && t.Trimmed == "--");
    }

    [Fact]
    public void Word_indices_point_at_the_word_tokens()
    {
        var passage = Tokenizer.Tokenize("16 For God");

        Assert.Equal(2, passage.WordCount);
        Assert.All(passage.WordIndices, i => Assert.True(passage[i].IsWord));
        Assert.Equal(["For", "God"], passage.WordIndices.Select(i => passage[i].Word));
    }

    [Fact]
    public void Empty_text_produces_no_tokens()
    {
        var passage = Tokenizer.Tokenize("");

        Assert.Empty(passage);
        Assert.Equal(0, passage.WordCount);
    }

    [Fact]
    public void Token_indices_are_sequential()
    {
        var passage = Tokenizer.Tokenize("For God so loved the world.");

        Assert.Equal(Enumerable.Range(0, passage.Count), passage.Select(t => t.Index));
    }
}
