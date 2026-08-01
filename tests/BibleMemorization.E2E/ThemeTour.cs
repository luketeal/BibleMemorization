using System.Text;
using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// Photographs the same views under every theme and builds a contact sheet, so
/// palettes can be compared against each other rather than judged one at a time.
///
/// A design tool rather than a regression test: it asserts nothing about colour, and
/// is gated behind E2E_THEME_TOUR=1 so twenty-odd full-page screenshots are not taken
/// on every CI run. Set the variable to run it.
/// </summary>
public sealed class ThemeTour(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private readonly ITestOutputHelper _out = output;

    private static readonly string[] Themes = ["light", "dark"];

    private static readonly ViewportSize Desktop = new() { Width = 1280, Height = 800 };
    private static readonly ViewportSize Mobile = new() { Width = 390, Height = 844 };

    /// <summary>
    /// The views worth comparing: between them they cover every distinct surface —
    /// cards and nav, list rows, the reading panel, inputs, all three outcome fills,
    /// forms, and the layout below the breakpoint.
    /// </summary>
    private static readonly (string Slug, string Label)[] Views =
    [
        ("01-home", "Home"),
        ("02-library", "Library"),
        ("03-practice-study", "Practice — study"),
        ("04-practice-test", "Practice — test"),
        ("05-practice-results", "Practice — results"),
        ("06-settings", "Settings"),
        ("07-mobile-practice", "Practice — mobile"),
    ];

    /// <summary>
    /// One test rather than a theory, because the contact sheet has to be written once
    /// after every shot exists and xUnit guarantees no ordering between test methods.
    /// </summary>
    [Fact]
    public async Task Capture_theme_variants()
    {
        if (Environment.GetEnvironmentVariable("E2E_THEME_TOUR") != "1")
        {
            _out.WriteLine("Skipped: set E2E_THEME_TOUR=1 to capture the theme contact sheet.");
            return;
        }

        var dir = RepoLayout.EnsureDir(RepoLayout.ThemeShotsDir);

        foreach (var theme in Themes)
        {
            await CaptureThemeAsync(theme, dir);
            _out.WriteLine($"Captured {Views.Length} views for '{theme}'.");
        }

        var sheet = Path.Combine(dir, "index.html");
        await File.WriteAllTextAsync(sheet, BuildContactSheet());
        _out.WriteLine($"Contact sheet: {sheet}");
    }

    private async Task CaptureThemeAsync(string theme, string dir)
    {
        // The theme rides on the query string, so it is applied by the inline script
        // during head parsing — before the first paint, and before any Blazor code
        // exists. That leaves no race to wait out and no flash to photograph.
        await Page.SetViewportSizeAsync(Desktop.Width, Desktop.Height);

        await GotoAsync($"?demo=1&theme={theme}");
        await ShotAsync(Name("01-home", theme), dir);

        await GotoAsync($"library?demo=1&theme={theme}");
        await ShotAsync(Name("02-library", theme), dir);

        await TourSteps.OpenFirstPassageAsync(Page);
        await TourSteps.HideKnownWordsAsync(Page);
        await ShotAsync(Name("03-practice-study", theme), dir);

        await Page.GetByTestId("mode-test").ClickAsync();
        await ShotAsync(Name("04-practice-test", theme), dir);

        await TourSteps.AnswerMixedAsync(Page);
        await ShotAsync(Name("05-practice-results", theme), dir);

        await GotoAsync($"settings?demo=1&theme={theme}");
        await ShotAsync(Name("06-settings", theme), dir);

        await Page.SetViewportSizeAsync(Mobile.Width, Mobile.Height);
        await GotoAsync($"library?demo=1&theme={theme}");
        await TourSteps.OpenFirstPassageAsync(Page);
        await TourSteps.HideKnownWordsAsync(Page);
        await ShotAsync(Name("07-mobile-practice", theme), dir);
    }

    /// <summary>
    /// Two hyphens between view and theme, so a theme name containing one still parses,
    /// and the view first so every theme of a view sorts next to the others.
    /// </summary>
    private static string Name(string view, string theme) => $"{view}--{theme}";

    private const string Style = """
        :root { color-scheme: dark; --bg: #14161b; --fg: #e8eaef; --muted: #949aa5; --line: #2e333d; }
        body { margin: 0; padding: 1.5rem; background: var(--bg); color: var(--fg);
               font: 15px/1.5 ui-sans-serif, system-ui, -apple-system, sans-serif; }
        h1 { font-size: 1.25rem; margin: 0 0 0.25rem; }
        p.meta { color: var(--muted); margin: 0 0 1.5rem; }
        .controls { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1.5rem; }
        button { font: inherit; color: var(--fg); background: #21242c; border: 1px solid var(--line);
                 border-radius: 6px; padding: 0.35rem 0.8rem; cursor: pointer; }
        button[aria-pressed="true"] { background: #7aa2f7; color: #10131a; border-color: #7aa2f7; }
        .grid { display: grid; gap: 1rem 1.25rem; align-items: start; }
        .head { position: sticky; top: 0; z-index: 2; background: var(--bg);
                padding: 0.5rem 0; font-weight: 600; text-transform: capitalize; }
        .rowlabel { color: var(--muted); font-size: 0.85rem; padding-top: 0.5rem; }
        figure { margin: 0; }
        img { width: 100%; display: block; border: 1px solid var(--line); border-radius: 8px;
              background: #fff; cursor: zoom-in; }
        /* Full-page shots get small side by side, so a click blows one up. */
        body.zoomed { overflow: hidden; }
        .zoom-layer { position: fixed; inset: 0; background: rgba(0,0,0,0.85); z-index: 10;
                      overflow: auto; padding: 1rem; display: none; }
        body.zoomed .zoom-layer { display: block; }
        .zoom-layer img { max-width: 1280px; margin: 0 auto; cursor: zoom-out; }
        """;

    private const string Script = """
        const themes = window.__themes;
        const grid = document.getElementById('grid');
        function layout(only) {
          const cols = only === 'all' ? themes : [only];
          grid.style.gridTemplateColumns = '7rem repeat(' + cols.length + ', 1fr)';
          document.querySelectorAll('[data-col]').forEach(el => {
            el.style.display = cols.includes(el.dataset.col) ? '' : 'none';
          });
        }
        document.querySelectorAll('.controls button').forEach(b => b.addEventListener('click', () => {
          document.querySelectorAll('.controls button')
            .forEach(o => o.setAttribute('aria-pressed', String(o === b)));
          layout(b.dataset.theme);
        }));
        const zoom = document.getElementById('zoom');
        grid.addEventListener('click', e => {
          if (e.target.tagName !== 'IMG') return;
          zoom.querySelector('img').src = e.target.src;
          document.body.classList.add('zoomed');
        });
        zoom.addEventListener('click', () => document.body.classList.remove('zoomed'));
        addEventListener('keydown', e => {
          if (e.key === 'Escape') document.body.classList.remove('zoomed');
        });
        layout('all');
        """;

    private static string BuildContactSheet()
    {
        var html = new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<!-- Generated by tests/BibleMemorization.E2E/ThemeTour.cs. Not checked in. -->");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>Bible Memorization — theme options</title>");
        html.AppendLine("<style>");
        html.AppendLine(Style);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<h1>Bible Memorization — theme options</h1>");
        html.AppendLine($"<p class=\"meta\">Captured {DateTime.Now:yyyy-MM-dd HH:mm}. "
            + "Rows are views, columns are themes. Click any shot to enlarge.</p>");

        html.AppendLine("<div class=\"controls\">");
        html.AppendLine("  <button data-theme=\"all\" aria-pressed=\"true\">Both</button>");
        foreach (var theme in Themes)
        {
            html.AppendLine($"  <button data-theme=\"{theme}\" aria-pressed=\"false\">{theme}</button>");
        }

        html.AppendLine("</div>");

        html.AppendLine("<div class=\"grid\" id=\"grid\">");
        html.AppendLine("  <div></div>");
        foreach (var theme in Themes)
        {
            html.AppendLine($"  <div class=\"head\" data-col=\"{theme}\">{theme}</div>");
        }

        foreach (var (slug, label) in Views)
        {
            html.AppendLine($"  <div class=\"rowlabel\">{label}</div>");
            foreach (var theme in Themes)
            {
                html.AppendLine($"  <figure data-col=\"{theme}\">"
                    + $"<img loading=\"lazy\" src=\"{Name(slug, theme)}.png\" alt=\"{label} — {theme}\">"
                    + "</figure>");
            }
        }

        html.AppendLine("</div>");
        html.AppendLine("<div class=\"zoom-layer\" id=\"zoom\"><img alt=\"\"></div>");
        html.AppendLine("<script>");
        html.AppendLine($"window.__themes = [{string.Join(", ", Themes.Select(t => $"'{t}'"))}];");
        html.AppendLine(Script);
        html.AppendLine("</script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }
}
