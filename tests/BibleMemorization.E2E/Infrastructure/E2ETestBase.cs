using System.Reflection;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E.Infrastructure;

/// <summary>
/// Per-test browser context and page, plus artifact capture.
/// </summary>
[Collection(AppCollection.Name)]
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly AppFixture _fixture;
    private readonly string _testName;
    private IBrowserContext _context = null!;

    protected IPage Page { get; private set; } = null!;

    /// <summary>Exposed for the rare test that needs its own context, e.g. a different locale.</summary>
    protected AppFixture Fixture => _fixture;

    protected string BaseUrl => _fixture.BaseUrl;

    protected E2ETestBase(AppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _testName = ResolveTestName(output, GetType());
    }

    public virtual async Task InitializeAsync()
    {
        _context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        });

        if (Environment.GetEnvironmentVariable("E2E_TRACE") == "1")
        {
            await _context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            });
        }

        Page = await _context.NewPageAsync();
    }

    /// <summary>
    /// Navigates and waits for Blazor to finish booting. Without the wait every
    /// assertion would race the WebAssembly runtime download.
    /// </summary>
    protected async Task GotoAsync(string path = "/")
    {
        var url = BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        await Page.GotoAsync(url);
        await Page.GetByTestId("app-ready").WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
    }

    /// <summary>
    /// Saves a named screenshot for review, under artifacts/screenshots unless another
    /// directory is given.
    /// </summary>
    protected async Task ShotAsync(string name, string? directory = null)
    {
        var dir = RepoLayout.EnsureDir(directory ?? RepoLayout.ScreenshotsDir);
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(dir, $"{name}.png"),
            FullPage = true,
        });
    }

    public virtual async Task DisposeAsync()
    {
        // Captured for every test, pass or fail. On a failure this teardown still
        // runs, so the image shows the page exactly as the assertion left it.
        try
        {
            var dir = RepoLayout.EnsureDir(RepoLayout.FailuresDir);
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(dir, $"{_testName}.png"),
                FullPage = true,
            });

            if (Environment.GetEnvironmentVariable("E2E_TRACE") == "1")
            {
                await _context.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = Path.Combine(dir, $"{_testName}.trace.zip"),
                });
            }
        }
        catch
        {
            // Artifact capture must never turn a passing test red, nor mask the
            // real failure of a red one.
        }

        await _context.DisposeAsync();
    }

    /// <summary>
    /// xUnit does not expose the running test's name directly, so it is read off
    /// the output helper. Any failure here falls back to the class name, since a
    /// slightly worse artifact filename is not worth breaking a test run over.
    /// </summary>
    private static string ResolveTestName(ITestOutputHelper output, Type testClass)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var test =
                output.GetType().GetProperty("Test", flags)?.GetValue(output)
                ?? output.GetType().GetField("test", flags)?.GetValue(output);

            if (test?.GetType().GetProperty("DisplayName")?.GetValue(test) is string displayName
                && !string.IsNullOrWhiteSpace(displayName))
            {
                var shortName = displayName.Split('.').Last();
                return Sanitize(shortName);
            }
        }
        catch
        {
            // Fall through to the class-name default.
        }

        return Sanitize(testClass.Name);
    }

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
        return cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }
}
