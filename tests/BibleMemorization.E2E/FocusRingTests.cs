using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// Guards against Bootstrap's own colours surviving the theme.
///
/// This is the one place the suite asserts on computed style rather than behaviour,
/// and the exception is deliberate. Bootstrap declares literal colours on individual
/// components — inside <c>.btn-primary</c>, inside each alert variant, on checkboxes,
/// on focus rings — which a <c>:root</c> bridge cannot reach. That has now escaped
/// three times, every time found by eye rather than by a test, because nothing here
/// looks at colour.
///
/// So the assertion is deliberately narrow: no focused control may draw a ring in
/// Bootstrap's default blue. It does not pin an exact shadow, which would break on
/// every legitimate palette change and train people to update the number without
/// reading it.
///
/// Known gap: a range slider draws its ring on <c>::-webkit-slider-thumb</c>, and
/// Chromium reports no computed style for that pseudo-element, so these tests cannot
/// see it. The sliders' rings are covered by CSS but verified by eye.
/// </summary>
public sealed class FocusRingTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    /// <summary>Bootstrap 5's default $primary, as Chromium reports it.</summary>
    private const string StockBlue = "13, 110, 253";

    private Task<string> RingAsync(string testId) => Page.EvaluateAsync<string>(
        "id => getComputedStyle(document.querySelector(`[data-testid=${id}]`)).boxShadow",
        testId);

    private async Task AssertRingIsThemedAsync(string testId)
    {
        // First: some testids repeat down a list, and any one of them will do.
        await Page.GetByTestId(testId).First.FocusAsync();
        var ring = await RingAsync(testId);

        Assert.False(
            ring.Contains(StockBlue, StringComparison.Ordinal),
            $"Focused '{testId}' draws Bootstrap's default blue: {ring}. Its colour needs "
            + "bridging onto a token — see the Bootstrap bridge block in app.css.");
    }

    [Theory]
    [InlineData("nav-home")]
    [InlineData("nav-library")]
    [InlineData("nav-settings")]
    public async Task Sidebar_links_are_themed_when_focused(string testId)
    {
        await GotoAsync("settings?demo=1");

        await AssertRingIsThemedAsync(testId);
    }

    [Theory]
    [InlineData("theme-picker")]
    [InlineData("retry-miss")]
    [InlineData("clear-all")]
    public async Task Settings_controls_are_themed_when_focused(string testId)
    {
        await GotoAsync("settings?demo=1");

        await AssertRingIsThemedAsync(testId);
    }

    [Fact]
    public async Task Buttons_and_fields_are_themed_when_focused()
    {
        await GotoAsync("import?demo=1");
        await AssertRingIsThemedAsync("reference-box");

        // The lookup button stays disabled until the box has something in it, and a
        // disabled control cannot take focus.
        await Page.GetByTestId("reference-box").FillAsync("John 3:16");
        await AssertRingIsThemedAsync("reference-lookup");

        await GotoAsync("library?demo=1");
        await AssertRingIsThemedAsync("practice-link");
    }

    [Fact]
    public async Task A_focused_control_still_draws_a_visible_ring()
    {
        // The cheapest way to "fix" the assertion above would be to remove the ring
        // altogether, which is worse than the wrong colour. This makes that fail too.
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("retry-miss").FocusAsync();

        var ring = await RingAsync("retry-miss");

        Assert.NotEqual("none", ring);
    }
}
