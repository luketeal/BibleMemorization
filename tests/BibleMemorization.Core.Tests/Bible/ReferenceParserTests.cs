using BibleMemorization.Core.Bible;

namespace BibleMemorization.Core.Tests.Bible;

public class ReferenceParserTests
{
    /// <summary>
    /// Deliberately includes both "John" and "1 John", plus several books starting
    /// "Jo", because those are where naive matching goes wrong.
    /// </summary>
    private static readonly IReadOnlyList<BibleBook> Books =
    [
        new() { Id = "GEN", Name = "Genesis", CommonName = "Genesis", Order = 1, NumberOfChapters = 50 },
        new() { Id = "PSA", Name = "Psalms", CommonName = "Psalms", Order = 19, NumberOfChapters = 150 },
        new() { Id = "JOL", Name = "Joel", CommonName = "Joel", Order = 29, NumberOfChapters = 3 },
        new() { Id = "JON", Name = "Jonah", CommonName = "Jonah", Order = 32, NumberOfChapters = 4 },
        new() { Id = "MAT", Name = "Matthew", CommonName = "Matthew", Order = 40, NumberOfChapters = 28 },
        new() { Id = "JHN", Name = "John", CommonName = "John", Order = 43, NumberOfChapters = 21 },
        new() { Id = "1CO", Name = "1 Corinthians", CommonName = "1 Corinthians", Order = 46, NumberOfChapters = 16 },
        new() { Id = "2CO", Name = "2 Corinthians", CommonName = "2 Corinthians", Order = 47, NumberOfChapters = 13 },
        new() { Id = "PHP", Name = "Philippians", CommonName = "Philippians", Order = 50, NumberOfChapters = 4 },
        new() { Id = "1JN", Name = "1 John", CommonName = "1 John", Order = 62, NumberOfChapters = 5 },
    ];

    private static BibleReference Parse(string input) =>
        ReferenceParser.Parse(input, Books) ?? throw new InvalidOperationException($"'{input}' did not parse.");

    [Fact]
    public void A_single_verse_parses()
    {
        var reference = Parse("John 3:16");

        Assert.Equal("JHN", reference.BookId);
        Assert.Equal(3, reference.Chapter);
        Assert.Equal(16, reference.FirstVerse);
        Assert.Null(reference.LastVerse);
        Assert.False(reference.IsWholeChapter);
    }

    [Fact]
    public void A_verse_range_parses()
    {
        var reference = Parse("John 3:16-18");

        Assert.Equal(16, reference.FirstVerse);
        Assert.Equal(18, reference.LastVerse);
    }

    [Theory]
    [InlineData("John 3:16–18")]
    [InlineData("John 3:16—18")]
    public void En_and_em_dashes_work_as_range_separators(string input)
    {
        // Phones and word processors substitute these silently.
        var reference = Parse(input);

        Assert.Equal(16, reference.FirstVerse);
        Assert.Equal(18, reference.LastVerse);
    }

    [Fact]
    public void A_whole_chapter_parses()
    {
        var reference = Parse("1 Corinthians 13");

        Assert.Equal("1CO", reference.BookId);
        Assert.Equal(13, reference.Chapter);
        Assert.True(reference.IsWholeChapter);
    }

    [Theory]
    [InlineData("1 Cor 13", "1CO")]
    [InlineData("1cor 13", "1CO")]
    [InlineData("I Corinthians 13", "1CO")]
    [InlineData("2 Cor 5", "2CO")]
    [InlineData("Ps 23", "PSA")]
    [InlineData("Psalm 23", "PSA")]
    [InlineData("psa 23", "PSA")]
    [InlineData("Gen 1", "GEN")]
    [InlineData("Matt 5", "MAT")]
    [InlineData("Mt 5", "MAT")]
    [InlineData("Jn 3", "JHN")]
    [InlineData("Phil 4", "PHP")]
    [InlineData("1 Jn 1", "1JN")]
    public void Abbreviations_resolve(string input, string expectedBookId)
    {
        Assert.Equal(expectedBookId, Parse(input).BookId);
    }

    [Theory]
    [InlineData("Gen. 1", "GEN")]
    [InlineData("Matt. 5:3", "MAT")]
    public void Trailing_full_stops_are_tolerated(string input, string expectedBookId)
    {
        Assert.Equal(expectedBookId, Parse(input).BookId);
    }

    [Theory]
    [InlineData("john 3:16")]
    [InlineData("JOHN 3:16")]
    [InlineData("  John   3 : 16  ")]
    public void Case_and_spacing_do_not_matter(string input)
    {
        Assert.Equal("JHN", Parse(input).BookId);
    }

    /// <summary>
    /// The trap this parser exists to avoid: "John" must never resolve to "1 John",
    /// which a prefix or contains match would happily do.
    /// </summary>
    [Fact]
    public void An_exact_book_name_wins_over_a_longer_one_containing_it()
    {
        Assert.Equal("JHN", Parse("John 3").BookId);
        Assert.Equal("1JN", Parse("1 John 3").BookId);
    }

    [Fact]
    public void An_ambiguous_prefix_is_refused_rather_than_guessed()
    {
        // "Jo" could be Joel or Jonah. Importing the wrong book silently would be
        // worse than falling back to the picker.
        Assert.Null(ReferenceParser.Parse("Jo 1", Books));
    }

    [Fact]
    public void An_unambiguous_prefix_still_resolves()
    {
        Assert.Equal("JON", Parse("Jona 1").BookId);
    }

    [Fact]
    public void A_backwards_range_is_read_the_way_it_was_meant()
    {
        var reference = Parse("John 3:18-16");

        Assert.Equal(16, reference.FirstVerse);
        Assert.Equal(18, reference.LastVerse);
    }

    [Fact]
    public void A_chapter_beyond_the_end_of_the_book_is_refused()
    {
        // Philippians has 4 chapters.
        Assert.Null(ReferenceParser.Parse("Philippians 9", Books));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not a reference")]
    [InlineData("Hezekiah 3:16")]
    [InlineData("12345")]
    [InlineData("John")]
    [InlineData("John 0")]
    [InlineData("John 3:0")]
    public void Junk_returns_null_so_the_caller_can_fall_back(string? input)
    {
        Assert.Null(ReferenceParser.Parse(input, Books));
    }

    [Fact]
    public void Parsing_against_an_empty_book_list_returns_null()
    {
        Assert.Null(ReferenceParser.Parse("John 3:16", []));
    }

    [Theory]
    [InlineData("John 3:16", "John 3:16")]
    [InlineData("John 3:16-18", "John 3:16-18")]
    [InlineData("1 Cor 13", "1 Corinthians 13")]
    [InlineData("Ps 23:1", "Psalms 23:1")]
    public void The_reference_renders_back_in_canonical_form(string input, string expected)
    {
        Assert.Equal(expected, Parse(input).ToString());
    }

    [Fact]
    public void A_single_verse_range_collapses_when_displayed()
    {
        Assert.Equal("John 3:16", Parse("John 3:16-16").ToString());
    }
}
