using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// Drives the speech flows through the demo-mode fakes. Headless Chromium has no
/// real speech recognition, so window.__bmTest.emitTranscript stands in for the
/// microphone. Everything above the browser API binding is exercised for real.
/// </summary>
public sealed class SpeechTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private Task SayAsync(string text) =>
        Page.EvaluateAsync("async (t) => await window.__bmTest.emitTranscript(t, true)", text);

    private Task<bool> IsMicOpenAsync() =>
        Page.EvaluateAsync<bool>("async () => await window.__bmTest.isListening()");

    private Task<string[]> SpokenAsync() =>
        Page.EvaluateAsync<string[]>("async () => await window.__bmTest.getSpokenText()");

    private async Task OpenPracticeAsync()
    {
        await GotoAsync("library?demo=1");
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Assertions.Expect(Page.GetByTestId("passage-view")).ToBeVisibleAsync();
    }

    // ---- Dictation while composing ----

    [Fact]
    public async Task Dictated_words_land_in_the_compose_box()
    {
        await GotoAsync("compose?demo=1");

        await Page.GetByTestId("dictation-toggle").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("dictation-status")).ToBeVisibleAsync();

        await SayAsync("I can do all things through Christ");

        await Assertions.Expect(Page.GetByTestId("compose-text"))
            .ToHaveValueAsync("I can do all things through Christ");
    }

    [Fact]
    public async Task Dictating_twice_appends_rather_than_replacing()
    {
        await GotoAsync("compose?demo=1");
        await Page.GetByTestId("dictation-toggle").ClickAsync();

        await SayAsync("The LORD is my shepherd;");
        await SayAsync("I shall not want.");

        await Assertions.Expect(Page.GetByTestId("compose-text"))
            .ToHaveValueAsync("The LORD is my shepherd; I shall not want.");
    }

    [Fact]
    public async Task A_dictated_passage_is_saved_as_dictated()
    {
        await GotoAsync("compose?demo=1");
        await Page.GetByTestId("dictation-toggle").ClickAsync();
        await SayAsync("For God so loved the world");
        await Page.GetByTestId("dictation-toggle").ClickAsync();

        await Page.GetByTestId("compose-title").FillAsync("Dictated verse");
        await Page.GetByTestId("compose-save").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("practice-heading")).ToBeVisibleAsync();

        await Page.GetByTestId("nav-library").ClickAsync();

        await Assertions.Expect(
            Page.Locator("[data-testid=passage-card]", new() { HasTextString = "Dictated verse" }))
            .ToContainTextAsync("Dictated");
    }

    // ---- Spoken free recitation ----

    [Fact]
    public async Task Reciting_the_passage_aloud_is_scored()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-speak").ClickAsync();

        await Page.GetByTestId("speak-toggle").ClickAsync();
        await SayAsync(
            "For God so loved the world that he gave his only begotten Son "
            + "that whosoever believeth in him should not perish but have everlasting life");
        await Page.GetByTestId("speak-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
        await Assertions.Expect(Page.GetByTestId("attempt-history")).ToContainTextAsync("spoken");
    }

    [Fact]
    public async Task A_partly_remembered_recitation_scores_partly()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-speak").ClickAsync();

        await Page.GetByTestId("speak-toggle").ClickAsync();
        await SayAsync("For God so loved the world");
        await Page.GetByTestId("speak-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("results-panel")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("results-accuracy")).Not.ToHaveTextAsync("100%");
    }

    // ---- Guided read-along ----

    [Fact]
    public async Task Read_along_speaks_only_the_words_still_showing()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        // It reads up to the blank, then waits rather than reading the hidden word.
        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });

        var spoken = await SpokenAsync();
        Assert.Equal("For", spoken[0]);
        Assert.DoesNotContain(spoken, s => s.Contains("God"));
    }

    /// <summary>
    /// The property the mode depends on: an open microphone during playback would
    /// transcribe the app's own voice and score it as the user's answer.
    /// </summary>
    [Fact]
    public async Task The_microphone_is_shut_while_the_app_is_reading()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });

        // Open only now, during the listen window.
        Assert.True(await IsMicOpenAsync());

        await SayAsync("God");

        // Closed again once it moves on to reading the rest.
        await Assertions.Expect(Page.GetByTestId("read-along-done"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
        Assert.False(await IsMicOpenAsync());
    }

    [Fact]
    public async Task Answering_a_blank_aloud_is_logged_as_correct()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });
        await SayAsync("God");

        // A correct answer shows in the passage where the blank was, rather than in
        // the log — the log is only for what went wrong.
        await Assertions.Expect(Page.GetByTestId("ra-filled"))
            .ToHaveTextAsync("God", new() { Timeout = 15_000 });
        await Assertions.Expect(Page.GetByTestId("read-along-done")).ToContainTextAsync("1 of 1");
        await Assertions.Expect(Page.GetByTestId("read-along-log")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task A_wrong_answer_is_logged_with_what_was_heard()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();

        // Turn off retries so one wrong answer settles it.
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("retry-miss").UncheckAsync();
        await Page.GetByTestId("nav-library").ClickAsync();
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });
        await SayAsync("dog");

        await Assertions.Expect(Page.GetByTestId("read-along-log"))
            .ToContainTextAsync("heard \"dog\"", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Read_along_with_nothing_hidden_just_reads_the_passage()
    {
        await OpenPracticeAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-done"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        var spoken = await SpokenAsync();
        Assert.Single(spoken);
        Assert.Contains("For God so loved the world", spoken[0]);
    }

    [Fact]
    public async Task Read_along_can_be_stopped_part_way()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });

        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-toggle")).ToHaveTextAsync("Start read-along");
        Assert.False(await IsMicOpenAsync());
    }

    // ---- The read-along passage view ----

    [Fact]
    public async Task Read_along_shows_the_passage_with_blanks_before_it_starts()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();

        // The text is readable straight away, exactly as in the fill-the-blanks view.
        await Assertions.Expect(Page.GetByTestId("read-along-passage"))
            .ToContainTextAsync("For");
        await Assertions.Expect(Page.GetByTestId("read-along-passage"))
            .ToContainTextAsync("so loved the world");
        await Assertions.Expect(Page.GetByTestId("ra-blank")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task The_hidden_word_is_not_given_away_by_the_passage_view()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();

        var text = await Page.GetByTestId("read-along-passage").InnerTextAsync();
        Assert.DoesNotContain("God", text);
    }

    /// <summary>
    /// What was asked for: the blank fills in with what you say, in place, as the
    /// read-along reaches it.
    /// </summary>
    [Fact]
    public async Task A_blank_fills_in_where_it_sits_once_spoken()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });
        await SayAsync("God");

        await Assertions.Expect(Page.GetByTestId("ra-filled"))
            .ToHaveTextAsync("God", new() { Timeout = 15_000 });

        // The passage now reads straight through.
        await Assertions.Expect(Page.GetByTestId("read-along-passage"))
            .ToContainTextAsync("For God so loved the world");
        await Assertions.Expect(Page.GetByTestId("ra-blank")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Words_appear_in_the_blank_while_they_are_still_being_spoken()
    {
        await OpenPracticeAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });

        // An interim guess: shown live, but not yet judged.
        await Page.EvaluateAsync(
            "async () => await window.__bmTest.emitTranscript('gaw', false)");

        await Assertions.Expect(Page.GetByTestId("ra-live")).ToHaveTextAsync("gaw");
        await Assertions.Expect(Page.GetByTestId("ra-filled")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task A_missed_word_is_revealed_in_place()
    {
        await OpenPracticeAsync();

        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("retry-miss").UncheckAsync();
        await Page.GetByTestId("nav-library").ClickAsync();
        await Page.GetByTestId("practice-link").First.ClickAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await Page.GetByTestId("read-along-toggle").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 15_000 });
        await SayAsync("dog");

        // The right word appears where the blank was, so the passage still reads
        // correctly, and the log says what was actually heard.
        await Assertions.Expect(Page.GetByTestId("ra-filled"))
            .ToHaveTextAsync("God", new() { Timeout = 15_000 });
        await Assertions.Expect(Page.GetByTestId("read-along-log")).ToContainTextAsync("heard \"dog\"");
    }
}
