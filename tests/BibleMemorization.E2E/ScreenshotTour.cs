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

        await GotoAsync("settings?demo=1");
        await ShotAsync("04-settings");
    }

    [Fact]
    public async Task Capture_mobile()
    {
        await Page.SetViewportSizeAsync(Mobile.Width, Mobile.Height);

        await GotoAsync("library?demo=1");
        await ShotAsync("mobile-01-library");

        await GotoAsync("settings?demo=1");
        await ShotAsync("mobile-02-settings");
    }
}
