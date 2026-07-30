using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// Runs against the bundled fixtures, since bible.helloao.org is unreachable from
/// the build container. The fixtures go through the same parsing and flattening as
/// the live client, so everything above the HTTP call is covered here.
/// </summary>
public sealed class ImportTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task A_reference_can_be_looked_up_and_saved()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("John 3:16-17");
        await Page.GetByTestId("reference-lookup").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("import-preview")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("preview-text"))
            .ToContainTextAsync("For God so loved the world");

        // The range must stop where the user asked, not run to the end of the chapter.
        await Assertions.Expect(Page.GetByTestId("preview-text"))
            .Not.ToContainTextAsync("condemned already");

        await Page.GetByTestId("import-save").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("practice-heading")).ToContainTextAsync("John 3:16-17");
    }

    [Fact]
    public async Task Headings_and_footnote_markers_stay_out_of_the_imported_text()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("John 3");
        await Page.GetByTestId("reference-lookup").ClickAsync();

        var text = await Page.GetByTestId("preview-text").InnerTextAsync();

        Assert.DoesNotContain("For God So Loved the World", text);
        Assert.DoesNotContain("Light and Darkness", text);
        Assert.DoesNotContain("noteId", text);
    }

    [Fact]
    public async Task Pressing_enter_looks_the_reference_up()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("Psalm 23");
        await Page.GetByTestId("reference-box").PressAsync("Enter");

        await Assertions.Expect(Page.GetByTestId("preview-text"))
            .ToContainTextAsync("The LORD is my shepherd");
    }

    [Fact]
    public async Task An_abbreviated_reference_resolves()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("1 Cor 13");
        await Page.GetByTestId("reference-lookup").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("import-preview")).ToContainTextAsync("1 Corinthians 13");
        await Assertions.Expect(Page.GetByTestId("preview-text")).ToContainTextAsync("tongues of men");
    }

    [Fact]
    public async Task An_unparseable_reference_points_the_user_at_the_pickers()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("Hezekiah 3:16");
        await Page.GetByTestId("reference-lookup").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("reference-error")).ToContainTextAsync("doesn't look like a reference");
    }

    [Fact]
    public async Task Browsing_by_book_and_chapter_works()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("book-picker").SelectOptionAsync("PSA");
        await Page.GetByTestId("chapter-picker").SelectOptionAsync("23");
        await Page.GetByTestId("browse-lookup").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("preview-text"))
            .ToContainTextAsync("The LORD is my shepherd");
    }

    /// <summary>
    /// Demo mode bundles three chapters, though the books they belong to advertise
    /// all of theirs. Asking for one of the others is an expected limit of the demo,
    /// not a failure, and has to read that way.
    /// </summary>
    [Fact]
    public async Task A_chapter_missing_from_the_demo_explains_itself()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("book-picker").SelectOptionAsync("JHN");
        await Page.GetByTestId("chapter-picker").SelectOptionAsync("1");
        await Page.GetByTestId("browse-lookup").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("import-error")).ToContainTextAsync("demo mode");
        await Assertions.Expect(Page.GetByTestId("import-preview")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task The_verse_range_can_be_narrowed_after_loading()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("Psalm 23");
        await Page.GetByTestId("reference-lookup").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("preview-text")).ToContainTextAsync("shepherd");

        await Page.GetByTestId("first-verse").SelectOptionAsync("4");

        var text = await Page.GetByTestId("preview-text").InnerTextAsync();
        Assert.Contains("valley of the shadow of death", text);
        Assert.DoesNotContain("green pastures", text);
    }

    [Fact]
    public async Task An_imported_passage_carries_its_translation_for_attribution()
    {
        await GotoAsync("import?demo=1");

        await Page.GetByTestId("reference-box").FillAsync("Psalm 23:1");
        await Page.GetByTestId("reference-lookup").ClickAsync();
        await Page.GetByTestId("import-save").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("practice-heading")).ToBeVisibleAsync();

        await Page.GetByTestId("nav-library").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("passage-card").First)
            .ToContainTextAsync("King James Version");
    }
}
