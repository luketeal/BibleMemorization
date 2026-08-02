using Microsoft.Playwright;

namespace BibleMemorization.E2E.Infrastructure;

/// <summary>
/// The click sequences that drive the practice view into a photographable state.
///
/// Shared by the tours rather than written out in each, so a theme shot always
/// depicts the same state as the baseline shot it is meant to be compared against.
/// Each step is deliberately deterministic — named words rather than the percentage
/// control, which hides a random tenth and would make two runs incomparable.
/// </summary>
public static class TourSteps
{
    /// <summary>Opens the first passage in the library and waits for the reading surface.</summary>
    public static async Task OpenFirstPassageAsync(IPage page)
    {
        await page.GetByTestId("practice-link").First.ClickAsync();
        await page.GetByTestId("passage-view").WaitForAsync();
    }

    /// <summary>
    /// Hides three known words, giving a realistic mid-session passage whose blanks
    /// are always the same three.
    /// </summary>
    public static async Task HideKnownWordsAsync(IPage page)
    {
        foreach (var word in new[] { "God", "world", "perish" })
        {
            await page.Locator("[data-testid=word]", new() { HasTextString = word }).First.ClickAsync();
        }
    }

    /// <summary>
    /// Answers one blank right and one wrong, leaving the third empty, so the results
    /// show all three outcomes at once.
    /// </summary>
    public static async Task AnswerMixedAsync(IPage page)
    {
        await page.GetByTestId("blank-input").Nth(0).FillAsync("God");
        await page.GetByTestId("blank-input").Nth(1).FillAsync("earth");
        await page.GetByTestId("check-answers").ClickAsync();
        await page.GetByTestId("results-panel").WaitForAsync();
    }
}
