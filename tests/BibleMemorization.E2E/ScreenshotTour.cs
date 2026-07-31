using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// Walks the app and photographs it, so the UI can be reviewed as it is built.
///
/// Runs in demo mode with a frozen clock and seeded passages, which keeps the
/// images stable enough to diff between runs.
/// </summary>
public sealed class ScreenshotTour(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private static readonly ViewportSize Mobile = new() { Width = 390, Height = 844 };

    private Task WaitForListeningAsync() =>
        Assertions.Expect(Page.GetByTestId("read-along-phase"))
            .ToContainTextAsync("Listening", new() { Timeout = 20_000 });

    [Fact]
    public async Task Capture_desktop()
    {
        await GotoAsync("?demo=1");
        await ShotAsync("01-home");

        await GotoAsync("library?demo=1");
        await ShotAsync("02-library");

        await GotoAsync("compose?demo=1");
        await Page.GetByTestId("compose-title").FillAsync("Philippians 4:13");
        await Page.GetByTestId("compose-text")
            .FillAsync("I can do all things through Christ which strengtheneth me.");
        await ShotAsync("03-compose");

        await GotoAsync("import?demo=1");
        await Page.GetByTestId("reference-box").FillAsync("Psalm 23:1-3");
        await Page.GetByTestId("reference-lookup").ClickAsync();
        await Page.GetByTestId("import-preview").WaitForAsync();
        await ShotAsync("04-import");

        await GotoAsync("settings?demo=1");
        await ShotAsync("05-settings");
    }

    [Fact]
    public async Task Capture_practice()
    {
        await GotoAsync("library?demo=1");
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Page.GetByTestId("passage-view").WaitForAsync();
        await ShotAsync("10-practice-study-nothing-hidden");

        // A realistic mid-session state: some words gone, most still there.
        await Page.GetByTestId("hide-more").ClickAsync();
        await Page.GetByTestId("hide-more").ClickAsync();
        await ShotAsync("11-practice-study-partly-hidden");

        // Hide two known words so the answers below can be deliberately mixed.
        await Page.GetByTestId("reveal-all").ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "world" }).First.ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "perish" }).First.ClickAsync();

        await Page.GetByTestId("mode-test").ClickAsync();
        await ShotAsync("12-practice-test-blanks");

        // One right, one wrong, one left blank, so the results show all three outcomes.
        await Page.GetByTestId("blank-input").Nth(0).FillAsync("God");
        await Page.GetByTestId("blank-input").Nth(1).FillAsync("earth");

        await Page.GetByTestId("check-answers").ClickAsync();
        await Page.GetByTestId("results-panel").WaitForAsync();
        await ShotAsync("13-practice-results");

        // Back to Study, where first-letter actually shows its hints.
        await Page.GetByTestId("mode-study").ClickAsync();
        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await Page.GetByTestId("hint").First.WaitForAsync();
        await ShotAsync("14-practice-first-letter");
    }

    [Fact]
    public async Task Capture_read_along()
    {
        await GotoAsync("library?demo=1");
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Page.GetByTestId("passage-view").WaitForAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "world" }).First.ClickAsync();

        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await ShotAsync("20-read-along-ready");

        await Page.GetByTestId("read-along-toggle").ClickAsync();

        // Each answer has to wait for its own listen window. Emitting while the app
        // is still reading is exactly what the coordinator ignores, so a screenshot
        // taken without this wait would show "nothing heard".
        await WaitForListeningAsync();
        await ShotAsync("21-read-along-listening");
        await Page.EvaluateAsync("async () => await window.__bmTest.emitTranscript('God', true)");

        // A correct answer now appears in the passage itself, not in the log.
        await Assertions.Expect(Page.GetByTestId("ra-filled"))
            .ToHaveTextAsync("God", new() { Timeout = 20_000 });

        await WaitForListeningAsync();
        await Page.EvaluateAsync("async () => await window.__bmTest.emitTranscript('earth', true)");

        await Page.GetByTestId("read-along-done").WaitForAsync(new() { Timeout = 30_000 });
        await ShotAsync("22-read-along-finished");
    }

    [Fact]
    public async Task Capture_read_along_mobile()
    {
        await Page.SetViewportSizeAsync(Mobile.Width, Mobile.Height);

        await GotoAsync("library?demo=1");
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Page.GetByTestId("passage-view").WaitForAsync();

        await Page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Page.Locator("[data-testid=word]", new() { HasTextString = "world" }).First.ClickAsync();

        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-read-along").ClickAsync();
        await ShotAsync("mobile-10-read-along-ready");

        await Page.GetByTestId("read-along-toggle").ClickAsync();
        await WaitForListeningAsync();
        await Page.EvaluateAsync("async () => await window.__bmTest.emitTranscript('God', true)");

        await Assertions.Expect(Page.GetByTestId("ra-filled"))
            .ToHaveTextAsync("God", new() { Timeout = 20_000 });
        await WaitForListeningAsync();
        await ShotAsync("mobile-11-read-along-filling");
    }

    [Fact]
    public async Task Capture_mobile()
    {
        await Page.SetViewportSizeAsync(Mobile.Width, Mobile.Height);

        await GotoAsync("library?demo=1");
        await ShotAsync("mobile-01-library");

        await GotoAsync("library?demo=1");
        await Page.GetByTestId("practice-link").First.ClickAsync();
        await Page.GetByTestId("passage-view").WaitForAsync();
        await Page.GetByTestId("hide-more").ClickAsync();
        await ShotAsync("mobile-02-practice");

        await GotoAsync("settings?demo=1");
        await ShotAsync("mobile-03-settings");
    }
}
