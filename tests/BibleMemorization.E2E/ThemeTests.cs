using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// The three ways a theme gets chosen, and the one rule about which of them wins.
///
/// These drive the inline boot script in index.html rather than any Blazor code, so
/// they assert on the attributes it sets rather than on colour.
/// </summary>
public sealed class ThemeTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private Task<string> ThemeAsync() =>
        Page.EvaluateAsync<string>("() => document.documentElement.dataset.theme");

    private Task<string> BootstrapThemeAsync() =>
        Page.EvaluateAsync<string>("() => document.documentElement.dataset.bsTheme");

    [Fact]
    public async Task A_theme_can_be_named_in_the_url()
    {
        await GotoAsync("?demo=1&theme=light");

        Assert.Equal("light", await ThemeAsync());
    }

    [Fact]
    public async Task A_dark_theme_hands_bootstrap_its_own_dark_mode()
    {
        await GotoAsync("?demo=1&theme=dark");

        Assert.Equal("dark", await BootstrapThemeAsync());
        Assert.Equal("dark", await Page.EvaluateAsync<string>(
            "() => document.documentElement.style.colorScheme"));
    }

    [Fact]
    public async Task An_unknown_theme_is_ignored_rather_than_applied()
    {
        await GotoAsync("?demo=1&theme=chartreuse");

        Assert.Equal("light", await ThemeAsync());
    }

    [Fact]
    public async Task A_stored_choice_survives_a_reload()
    {
        await GotoAsync("?demo=1");
        await Page.EvaluateAsync(
            "() => window.localStorage.setItem('biblememorization.theme', 'dark')");

        await GotoAsync("?demo=1");

        Assert.Equal("dark", await ThemeAsync());
    }

    [Fact]
    public async Task A_theme_named_in_the_url_is_not_written_to_storage()
    {
        // Otherwise a screenshot run or a shared link would overwrite a real preference.
        await GotoAsync("?demo=1&theme=dark");

        Assert.Null(await Page.EvaluateAsync<string?>(
            "() => window.localStorage.getItem('biblememorization.theme')"));
    }

    [Fact]
    public async Task A_stored_choice_beats_the_system_preference()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });

        await GotoAsync("?demo=1");
        await Page.EvaluateAsync(
            "() => window.localStorage.setItem('biblememorization.theme', 'light')");

        await GotoAsync("?demo=1");

        Assert.Equal("light", await ThemeAsync());
    }

    [Fact]
    public async Task With_no_choice_stored_the_system_preference_is_followed()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });

        await GotoAsync("?demo=1");

        Assert.Equal("dark", await ThemeAsync());
    }

    [Fact]
    public async Task A_light_system_preference_gets_a_light_theme()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });

        await GotoAsync("?demo=1");

        Assert.Equal("light", await BootstrapThemeAsync());
    }

    [Fact]
    public async Task The_settings_picker_applies_a_theme()
    {
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("theme-picker").SelectOptionAsync("dark");

        Assert.Equal("dark", await ThemeAsync());
        Assert.Equal("dark", await BootstrapThemeAsync());
    }

    [Fact]
    public async Task A_theme_chosen_in_settings_survives_leaving_the_page()
    {
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("theme-picker").SelectOptionAsync("dark");

        await Page.GetByTestId("nav-library").ClickAsync();
        await Page.GetByTestId("nav-settings").ClickAsync();

        Assert.Equal("dark", await ThemeAsync());
        await Assertions.Expect(Page.GetByTestId("theme-picker")).ToHaveValueAsync("dark");
    }

    [Fact]
    public async Task The_picker_opens_showing_a_previously_stored_choice()
    {
        await GotoAsync("?demo=1");
        await Page.EvaluateAsync(
            "() => window.localStorage.setItem('biblememorization.theme', 'dark')");

        await GotoAsync("settings?demo=1");

        await Assertions.Expect(Page.GetByTestId("theme-picker")).ToHaveValueAsync("dark");
    }

    [Fact]
    public async Task Matching_the_device_clears_the_stored_choice()
    {
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("theme-picker").SelectOptionAsync("dark");

        await Page.GetByTestId("theme-picker").SelectOptionAsync("system");

        Assert.Null(await Page.EvaluateAsync<string?>(
            "() => window.localStorage.getItem('biblememorization.theme')"));
    }

    [Fact]
    public async Task Matching_the_device_is_not_hijacked_by_a_theme_in_the_url()
    {
        // Resolution normally lets the URL win, which is what makes the screenshot
        // tour work. But "match my device" has to mean the device — otherwise choosing
        // it on a ?theme= page would appear to do nothing at all.
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await GotoAsync("settings?demo=1&theme=dark");
        Assert.Equal("dark", await ThemeAsync());

        await Page.GetByTestId("theme-picker").SelectOptionAsync("system");

        Assert.Equal("light", await ThemeAsync());
    }

    [Fact]
    public async Task The_hint_names_the_theme_actually_on_screen()
    {
        // Nothing is stored, so the picker reads "match my device" either way; the
        // hint is what tells you which way that resolved.
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await GotoAsync("settings?demo=1");

        await Assertions.Expect(Page.GetByTestId("theme-applied")).ToContainTextAsync("dark");
    }
}
