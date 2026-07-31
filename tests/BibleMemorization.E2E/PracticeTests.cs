using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

public sealed class PracticeTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    /// <summary>Opens the seeded John 3:16 passage in practice.</summary>
    private async Task OpenPracticeAsync()
    {
        await GotoAsync("library?demo=1");
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Assertions.Expect(Page.GetByTestId("passage-view")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_passage_opens_with_every_word_showing()
    {
        await OpenPracticeAsync();

        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByTestId("word").First).ToHaveTextAsync("For");
    }

    [Fact]
    public async Task Clicking_a_word_makes_it_vanish_and_clicking_the_blank_brings_it_back()
    {
        await OpenPracticeAsync();

        await Page.GetByTestId("word").Nth(1).ClickAsync();

        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.GetByTestId("practice-meta")).ToContainTextAsync("1 hidden");

        await Page.GetByTestId("blank").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// The blank keeps the width of the word it replaced, which is a real part of
    /// what makes the technique learnable rather than a styling detail.
    /// </summary>
    [Fact]
    public async Task A_blank_is_sized_to_the_word_it_hides()
    {
        await OpenPracticeAsync();

        // "world" (5 letters) against "so" (2 letters).
        await Page.Locator("[data-testid=word]", new() { HasTextString = "world" }).First.ClickAsync();
        var wide = await Page.GetByTestId("blank").First.BoundingBoxAsync();

        await Page.GetByTestId("reveal-all").ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "so" }).First.ClickAsync();
        var narrow = await Page.GetByTestId("blank").First.BoundingBoxAsync();

        Assert.True(wide!.Width > narrow!.Width,
            $"Blank for 'world' ({wide.Width}) should be wider than for 'so' ({narrow.Width}).");
    }

    [Fact]
    public async Task Bulk_controls_hide_and_reveal()
    {
        await OpenPracticeAsync();

        await Page.GetByTestId("hide-all").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("word")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByTestId("practice-meta")).ToContainTextAsync("25 hidden");

        await Page.GetByTestId("reveal-all").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Hiding_ten_percent_hides_a_proportional_number()
    {
        await OpenPracticeAsync();

        await Page.GetByTestId("hide-more").ClickAsync();

        // 25 words, 10% rounded up.
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task Hidden_words_survive_navigating_away_and_back()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("hide-one").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(1);

        await Page.GetByTestId("nav-library").ClickAsync();
        await Page.GetByTestId("practice-link").First.ClickAsync();

        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(1);
    }

    // ---- Typed testing ----

    [Fact]
    public async Task Filling_every_blank_correctly_scores_full_marks()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        await Page.GetByTestId("blank-input").FillAsync("God");
        await Page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
        await Assertions.Expect(Page.GetByTestId("results-perfect")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_wrong_answer_is_marked_and_scored()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        await Page.GetByTestId("blank-input").FillAsync("dog");
        await Page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("0%");
        await Assertions.Expect(Page.GetByTestId("results-panel")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Case_and_punctuation_are_forgiven_when_checking()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "world" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        await Page.GetByTestId("blank-input").FillAsync("WORLD.");
        await Page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
    }

    [Fact]
    public async Task Writing_the_passage_out_is_scored_against_the_whole_text()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-recite").ClickAsync();

        await Page.GetByTestId("recite-box").FillAsync(
            "For God so loved the world, that he gave his only begotten Son, "
            + "that whosoever believeth in him should not perish, but have everlasting life.");

        await Page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
    }

    /// <summary>
    /// The reason scoring aligns rather than compares position by position: one
    /// dropped word early on must not invalidate everything after it.
    /// </summary>
    [Fact]
    public async Task Dropping_one_word_while_reciting_only_costs_that_word()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-recite").ClickAsync();

        await Page.GetByTestId("recite-box").FillAsync(
            "For God loved the world, that he gave his only begotten Son, "
            + "that whosoever believeth in him should not perish, but have everlasting life.");

        await Page.GetByTestId("check-answers").ClickAsync();

        // 24 of 25 words, not a cascade of failures.
        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("96%");
    }

    [Fact]
    public async Task Test_mode_says_so_when_nothing_is_hidden()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("nothing-hidden")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Attempts_are_recorded_in_the_history()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-recite").ClickAsync();
        await Page.GetByTestId("recite-box").FillAsync("For God so loved the world");
        await Page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("attempt-history")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("attempt-history")).ToContainTextAsync("typed");
    }

    // ---- Techniques ----

    /// <summary>
    /// An initial is only half the cue; the other half is how long the word is. All
    /// hints rendered at one width would throw that away, so a long word's hint has
    /// to be visibly wider than a short one's.
    /// </summary>
    [Fact]
    public async Task A_first_letter_hint_is_as_wide_as_the_word_it_stands_for()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();

        // Token 23 is "everlasting" (11 letters); token 2 is "so" (2 letters).
        var longWord = await Page.Locator("[data-testid=hint][data-token-index='23']").BoundingBoxAsync();
        var shortWord = await Page.Locator("[data-testid=hint][data-token-index='2']").BoundingBoxAsync();

        Assert.True(longWord!.Width > shortWord!.Width * 2,
            $"Hint for 'everlasting' ({longWord.Width}) should be much wider than for 'so' ({shortWord.Width}).");
    }

    [Fact]
    public async Task First_letter_mode_collapses_every_word_to_its_initial()
    {
        await OpenPracticeAsync();

        await Page.GetByTestId("technique-first-letter").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("hint").First).ToHaveTextAsync("F");
        await Assertions.Expect(Page.GetByTestId("hint")).ToHaveCountAsync(25);
        // Nothing is dropped yet, so this is the technique at its gentlest.
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Progress is keyed by passage and technique together, so each keeps its own
    /// hidden words.
    /// </summary>
    [Fact]
    public async Task Switching_technique_does_not_disturb_the_others_hidden_words()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("hide-all").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(25);

        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("hint")).ToHaveCountAsync(25);

        await Page.GetByTestId("technique-vanishing-text").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(25);
    }

    [Fact]
    public async Task An_unknown_passage_says_so_instead_of_breaking()
    {
        await GotoAsync("practice/00000000-0000-0000-0000-000000000000?demo=1");

        await Assertions.Expect(Page.GetByTestId("practice-missing")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Guards the regression directly in the browser. Blazor WebAssembly takes its
    /// culture from the browser, and a "P0" format rendered "100 %" with a space
    /// under some locales - which passed locally and failed in CI. A French locale
    /// is a locale that formats percentages with a space, so this fails if the
    /// culture-dependent format ever comes back.
    /// </summary>
    [Fact]
    public async Task Accuracy_reads_the_same_under_a_non_english_locale()
    {
        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "fr-FR",
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/library?demo=1");
        await page.GetByTestId("app-ready").WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });

        await page.GetByTestId("practice-link").First.ClickAsync();
        await page.GetByTestId("mode-test").ClickAsync();
        await page.GetByTestId("input-recite").ClickAsync();
        await page.GetByTestId("recite-box").FillAsync(
            "For God so loved the world, that he gave his only begotten Son, "
            + "that whosoever believeth in him should not perish, but have everlasting life.");
        await page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
    }

    // ---- First letter as a technique you can practise, not just look at ----

    [Fact]
    public async Task First_letter_offers_study_controls_in_its_own_words()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("hide-all")).ToContainTextAsync("initials");
        await Assertions.Expect(Page.GetByTestId("reveal-all")).ToContainTextAsync("initials");
    }

    [Fact]
    public async Task Tapping_an_initial_drops_it_and_tapping_the_blank_restores_it()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("hint")).ToHaveCountAsync(25);

        await Page.GetByTestId("hint").First.ClickAsync();

        await Assertions.Expect(Page.GetByTestId("hint")).ToHaveCountAsync(24);
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(1);

        await Page.GetByTestId("blank").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("hint")).ToHaveCountAsync(25);
    }

    [Fact]
    public async Task Dropping_every_initial_leaves_only_blanks()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();

        await Page.GetByTestId("hide-all").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("hint")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByTestId("blank")).ToHaveCountAsync(25);
    }

    /// <summary>
    /// The point of the technique: the initial is the cue you recall from, so a test
    /// that hides it is not testing first letter at all.
    /// </summary>
    [Fact]
    public async Task Testing_first_letter_keeps_the_initial_visible_beside_each_input()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("blank-input")).ToHaveCountAsync(25);
        await Assertions.Expect(Page.GetByTestId("blank-cue")).ToHaveCountAsync(25);
        await Assertions.Expect(Page.GetByTestId("blank-cue").First).ToHaveTextAsync("F");
    }

    [Fact]
    public async Task A_dropped_initial_leaves_its_input_without_a_cue()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Page.GetByTestId("hint").First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        // Still tested, just with nothing to go on.
        await Assertions.Expect(Page.GetByTestId("blank-input")).ToHaveCountAsync(25);
        await Assertions.Expect(Page.GetByTestId("blank-cue")).ToHaveCountAsync(24);
    }

    [Fact]
    public async Task Filling_in_the_words_under_first_letter_scores_them()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        var words = "For God so loved the world that he gave his only begotten Son that whosoever "
            + "believeth in him should not perish but have everlasting life";
        var expected = words.Split(' ');

        for (var i = 0; i < expected.Length; i++)
        {
            await Page.GetByTestId("blank-input").Nth(i).FillAsync(expected[i]);
        }

        await Page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
    }

    /// <summary>
    /// Read-along means "the app reads what is still showing". First letter never
    /// shows a word in full, so offering it would be offering something broken.
    /// </summary>
    [Fact]
    public async Task Read_along_is_offered_for_vanishing_text_but_not_for_first_letter()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("input-read-along")).ToHaveCountAsync(1);

        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("input-read-along")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Switching_to_first_letter_while_reading_along_lands_somewhere_usable()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("read-along")).ToBeVisibleAsync();

        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();

        // Not stranded on a panel the new technique does not offer.
        await Assertions.Expect(Page.GetByTestId("read-along")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByTestId("blank-input").First).ToBeVisibleAsync();
    }
}
