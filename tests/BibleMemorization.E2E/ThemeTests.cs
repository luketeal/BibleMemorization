using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// The two ways a theme gets chosen — a stored choice, or the device — and the rule
/// about which of them wins.
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

    private Task StoreAsync(string theme) => Page.EvaluateAsync(
        "t => window.localStorage.setItem('biblememorization.theme', t)", theme);

    private Task<string?> StoredAsync() => Page.EvaluateAsync<string?>(
        "() => window.localStorage.getItem('biblememorization.theme')");

    [Fact]
    public async Task A_dark_theme_hands_bootstrap_its_own_dark_mode()
    {
        await GotoAsync("?demo=1");
        await StoreAsync("dark");

        await GotoAsync("?demo=1");

        Assert.Equal("dark", await BootstrapThemeAsync());
        Assert.Equal("dark", await Page.EvaluateAsync<string>(
            "() => document.documentElement.style.colorScheme"));
    }

    [Fact]
    public async Task An_unrecognized_stored_theme_falls_back_to_the_device()
    {
        // The stored value is not trusted: it can be hand-edited, and it can be left
        // over from a build whose themes were named differently.
        await GotoAsync("?demo=1");
        await StoreAsync("chartreuse");

        await GotoAsync("?demo=1");

        Assert.Equal("light", await ThemeAsync());
    }

    [Fact]
    public async Task A_stored_choice_survives_a_reload()
    {
        await GotoAsync("?demo=1");
        await StoreAsync("dark");

        await GotoAsync("?demo=1");

        Assert.Equal("dark", await ThemeAsync());
    }

    [Fact]
    public async Task A_stored_choice_beats_the_system_preference()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });

        await GotoAsync("?demo=1");
        await StoreAsync("light");

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
        await StoreAsync("dark");

        await GotoAsync("settings?demo=1");

        await Assertions.Expect(Page.GetByTestId("theme-picker")).ToHaveValueAsync("dark");
    }

    [Fact]
    public async Task Matching_the_device_clears_the_choice_and_reverts_the_theme()
    {
        // Both halves matter. Clearing the key is what makes the device authoritative
        // again on the next load; reverting the theme now is what makes the setting
        // appear to do something, and is why set(null) applies system() rather than
        // resolve() — resolve() would re-read a key whose removal had failed.
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("theme-picker").SelectOptionAsync("dark");
        Assert.Equal("dark", await ThemeAsync());

        await Page.GetByTestId("theme-picker").SelectOptionAsync("system");

        Assert.Null(await StoredAsync());
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
