using Microsoft.Playwright;

namespace BibleMemorization.E2E.Infrastructure;

/// <summary>
/// Shared across the whole E2E collection: one app host and one browser, since
/// starting either per test would dominate the run time. Isolation between tests
/// comes from a fresh <see cref="IBrowserContext"/> instead.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private AppHost? _appHost;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl => _appHost!.BaseUrl;

    public async Task InitializeAsync()
    {
        _appHost = new AppHost();
        await _appHost.StartAsync();

        _playwright = await Playwright.CreateAsync();

        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = ResolveChromiumPath(),
            // The container runs as root, where Chromium's sandbox cannot start.
            Args = ["--no-sandbox", "--disable-dev-shm-usage"],
        });
    }

    /// <summary>
    /// Locally the container already ships a Chromium that matches this Playwright
    /// version, so we point straight at it. In CI the standard browser install runs
    /// instead and Playwright resolves its own copy, hence the null fallback.
    /// </summary>
    private static string? ResolveChromiumPath()
    {
        var configured = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        const string preinstalled = "/opt/pw-browsers/chromium";
        return File.Exists(preinstalled) ? preinstalled : null;
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_appHost is not null)
        {
            await _appHost.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
