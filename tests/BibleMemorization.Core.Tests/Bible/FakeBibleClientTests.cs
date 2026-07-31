using BibleMemorization.Core.Bible;

namespace BibleMemorization.Core.Tests.Bible;

/// <summary>
/// The fixtures go through the same deserialization and flattening as the live
/// client, so these tests cover that path too.
/// </summary>
public class FakeBibleClientTests
{
    private readonly FakeBibleClient _client = new();

    [Fact]
    public void It_reports_itself_as_offline()
    {
        Assert.False(_client.IsLive);
    }

    [Fact]
    public async Task Translations_load_from_the_bundled_fixture()
    {
        var translations = await _client.GetTranslationsAsync();

        Assert.Contains(translations, t => t.Id == "eng_kjv");
        Assert.Equal("King James Version", translations.Single(t => t.Id == "eng_kjv").DisplayName);
    }

    [Fact]
    public async Task Books_load_with_their_chapter_counts()
    {
        var books = await _client.GetBooksAsync("eng_kjv");

        var john = books.Single(b => b.Id == "JHN");
        Assert.Equal("John", john.DisplayName);
        Assert.Equal(21, john.NumberOfChapters);
    }

    [Fact]
    public async Task A_chapter_flattens_to_clean_verses()
    {
        var verses = await _client.GetChapterAsync("eng_kjv", "PSA", 23);

        Assert.Equal(6, verses.Count);
        Assert.Equal("The LORD is my shepherd; I shall not want.", verses[0].Text);
        Assert.Equal(1, verses[0].Number);
    }

    [Fact]
    public async Task Headings_are_left_out_of_the_text()
    {
        var verses = await _client.GetChapterAsync("eng_kjv", "JHN", 3);

        // "For God So Loved the World" is an editor's heading, not scripture.
        Assert.DoesNotContain(verses, v => v.Text.Contains("For God So Loved the World"));
        Assert.DoesNotContain(verses, v => v.Text.Contains("Light and Darkness"));
    }

    [Fact]
    public async Task Footnote_markers_never_reach_the_text()
    {
        var verses = await _client.GetChapterAsync("eng_kjv", "JHN", 3);

        // A stray marker would become a "word" the user is asked to recall.
        var john316 = verses.Single(v => v.Number == 16);
        Assert.DoesNotContain("noteId", john316.Text);
        Assert.DoesNotContain("0", john316.Text);
        Assert.Equal(
            "For God so loved the world, that he gave his only begotten Son, "
            + "that whosoever believeth in him should not perish, but have everlasting life.",
            john316.Text);
    }

    [Fact]
    public async Task Verses_split_across_several_parts_are_rejoined()
    {
        var verses = await _client.GetChapterAsync("eng_kjv", "PSA", 23);

        Assert.Equal(
            "He maketh me to lie down in green pastures: he leadeth me beside the still waters.",
            verses.Single(v => v.Number == 2).Text);
    }

    [Fact]
    public async Task A_verse_range_can_be_taken_from_a_chapter()
    {
        var verses = await _client.GetChapterAsync("eng_kjv", "JHN", 3);

        var text = ChapterFlattener.ToPlainText(verses, firstVerse: 16, lastVerse: 17);

        Assert.StartsWith("For God so loved the world", text);
        Assert.EndsWith("might be saved.", text);
        Assert.DoesNotContain("condemned already", text);
    }

    [Fact]
    public async Task A_chapter_that_is_not_bundled_comes_back_empty_rather_than_throwing()
    {
        // Demo mode ships only a few chapters; the import page reports this as
        // "not available offline" instead of an error.
        var verses = await _client.GetChapterAsync("eng_kjv", "GEN", 1);

        Assert.Empty(verses);
    }

    [Fact]
    public async Task All_advertised_chapters_actually_load()
    {
        foreach (var (translation, book, chapter, reference) in FakeBibleClient.Available)
        {
            var verses = await _client.GetChapterAsync(translation, book, chapter);

            Assert.True(verses.Count > 0, $"{reference} was advertised but returned no verses.");
        }
    }
}
