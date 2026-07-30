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

        await Page.GetByTestId("technique-first-letter").ClickAsync();
        await ShotAsync("14-practice-first-letter");
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
