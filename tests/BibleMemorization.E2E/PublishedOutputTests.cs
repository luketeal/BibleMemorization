using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// Runs against the real Release build, served as static files.
///
/// Everything else here uses the dev server, which builds in Debug and so cannot
/// catch trimming failures — the deployed app's most dangerous bug class, because
/// it works locally right up until it does not. These tests deliberately exercise
/// serialization, which is where trimming bites.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class PublishedOutputTests(AppFixture fixture, ITestOutputHelper output) : IAsyncLifetime
{
    private readonly StaticSiteHost _site = new();
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        await _site.StartAsync();

        _context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        });

        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            var dir = RepoLayout.EnsureDir(RepoLayout.FailuresDir);
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(dir, "PublishedOutput.png"),
                FullPage = true,
            });
        }
        catch
        {
            // Never let artifact capture affect the result.
        }

        await _context.DisposeAsync();
        await _site.DisposeAsync();
    }

    private async Task GotoAsync(string path)
    {
        await _page.GotoAsync($"{_site.BaseUrl}/{path.TrimStart('/')}");
        await _page.GetByTestId("app-ready").WaitForAsync(new LocatorWaitForOptions { Timeout = 90_000 });
    }

    [Fact]
    public async Task The_trimmed_app_boots_without_console_errors()
    {
        var errors = new List<string>();
        _page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };

        await GotoAsync("?demo=1");

        await Assertions.Expect(_page.GetByTestId("home-heading")).ToBeVisibleAsync();
        Assert.Empty(errors);
    }

    /// <summary>
    /// The specific failure trimming causes: reflection-based System.Text.Json is
    /// stripped, so serialization throws only in the published build. Source
    /// generation is what prevents it, and this proves it holds.
    /// </summary>
    [Fact]
    public async Task Serialization_survives_trimming()
    {
        await GotoAsync("?demo=1");

        var json = await _page.EvaluateAsync<string>(
            "async () => await window.__bmTest.getExportedSave()");

        Assert.Contains("John 3:16", json);
        Assert.Contains("schemaVersion", json);

        // Round-trip it back in, which exercises deserialization too.
        await _page.EvaluateAsync("async (j) => await window.__bmTest.seedLibrary(j)", json);

        await _page.GotoAsync($"{_site.BaseUrl}/library?demo=1");
        await Assertions.Expect(_page.GetByTestId("passage-card")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task Bible_fixtures_still_deserialize_in_the_trimmed_build()
    {
        await GotoAsync("import?demo=1");

        await _page.GetByTestId("reference-box").FillAsync("Psalm 23:1");
        await _page.GetByTestId("reference-lookup").ClickAsync();

        await Assertions.Expect(_page.GetByTestId("preview-text"))
            .ToContainTextAsync("The LORD is my shepherd");
    }

    /// <summary>
    /// GitHub Pages serves 404.html for unknown paths, and the workflow makes that a
    /// copy of the app shell. This checks a deep link loads the app rather than a
    /// dead end.
    /// </summary>
    [Fact]
    public async Task A_deep_link_loads_the_app_rather_than_a_dead_end()
    {
        await GotoAsync("library?demo=1");

        await Assertions.Expect(_page.GetByTestId("library-heading")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByTestId("passage-card")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task Practice_works_end_to_end_in_the_trimmed_build()
    {
        await GotoAsync("library?demo=1");
        await _page.GetByTestId("practice-link").First.ClickAsync();

        await _page.Locator("[data-testid=word]", new() { HasTextString = "God" }).First.ClickAsync();
        await Assertions.Expect(_page.GetByTestId("blank")).ToHaveCountAsync(1);

        await _page.GetByTestId("mode-test").ClickAsync();
        await _page.GetByTestId("blank-input").FillAsync("God");
        await _page.GetByTestId("check-answers").ClickAsync();

        await Assertions.Expect(_page.GetByTestId("results-accuracy")).ToHaveTextAsync("100%");
    }
}
