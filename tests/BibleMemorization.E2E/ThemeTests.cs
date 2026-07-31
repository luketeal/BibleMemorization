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
        await GotoAsync("?demo=1&theme=slate");

        Assert.Equal("slate", await ThemeAsync());
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

        Assert.Equal("blue", await ThemeAsync());
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
            "() => window.localStorage.setItem('biblememorization.theme', 'slate')");

        await GotoAsync("?demo=1");

        Assert.Equal("slate", await ThemeAsync());
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
}
