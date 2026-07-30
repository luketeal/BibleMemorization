using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Text;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Thou,", "thou")]
    [InlineData("\"Quoted\"", "quoted")]
    [InlineData("LORD", "lord")]
    [InlineData("God's", "gods")]
    [InlineData("loving-kindness", "lovingkindness")]
    [InlineData("Café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Words_normalize_to_a_comparable_form(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.NormalizeWord(input));
    }

    [Theory]
    [InlineData("Thou,", "thou")]
    [InlineData("God's", "gods")]
    // Speech recognizers return no punctuation, so the transcript form must match.
    [InlineData("loving-kindness", "lovingkindness")]
    [InlineData("Café", "cafe")]
    public void Punctuation_and_case_differences_still_match(string left, string right)
    {
        Assert.True(TextNormalizer.WordsMatch(left, right));
    }

    [Theory]
    [InlineData("shepherd", "shepard")]
    [InlineData("world", "word")]
    [InlineData("", "anything")]
    public void Genuinely_different_words_do_not_match(string left, string right)
    {
        Assert.False(TextNormalizer.WordsMatch(left, right));
    }

    [Fact]
    public void Empty_words_never_match_each_other()
    {
        // Guards the scorer: two unrecognised blanks must not count as a hit.
        Assert.False(TextNormalizer.WordsMatch("", ""));
        Assert.False(TextNormalizer.WordsMatch("...", "???"));
    }

    [Fact]
    public void Text_splits_into_normalized_words()
    {
        var words = TextNormalizer.NormalizeWords("For God so loved the world, that he gave...");

        Assert.Equal(["for", "god", "so", "loved", "the", "world", "that", "he", "gave"], words);
    }
}
