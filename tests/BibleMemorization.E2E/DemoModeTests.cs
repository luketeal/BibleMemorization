using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

public sealed class DemoModeTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task Demo_mode_seeds_a_library_and_says_so()
    {
        await GotoAsync("library?demo=1");

        await Assertions.Expect(Page.GetByTestId("demo-banner")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("passage-card")).ToHaveCountAsync(2);
        await Assertions.Expect(Page.GetByTestId("passage-card").First).ToContainTextAsync("John 3:16");
    }

    [Fact]
    public async Task The_real_app_starts_with_an_empty_library()
    {
        await GotoAsync("library");

        await Assertions.Expect(Page.GetByTestId("demo-banner")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByTestId("library-empty")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Demo mode lives in the query string, so a link that dropped it would put the
    /// user back in the real app mid-session.
    /// </summary>
    [Fact]
    public async Task Navigating_within_demo_mode_stays_in_demo_mode()
    {
        await GotoAsync("library?demo=1");

        await Page.GetByTestId("nav-compose").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("compose-heading")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("demo-banner")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Test_hooks_are_available_only_in_demo_mode()
    {
        await GotoAsync("library?demo=1");
        Assert.True(await Page.EvaluateAsync<bool>("() => !!window.__bmTest?.ready"));

        await GotoAsync("library");
        Assert.False(await Page.EvaluateAsync<bool>("() => !!window.__bmTest?.ready"));
    }

    [Fact]
    public async Task A_library_can_be_seeded_through_the_test_hook()
    {
        await GotoAsync("library?demo=1");

        await Page.EvaluateAsync(
            """
            async (json) => await window.__bmTest.seedLibrary(json)
            """,
            SeededLibraryJson);

        await Assertions.Expect(Page.GetByTestId("passage-card")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.GetByTestId("passage-card")).ToContainTextAsync("Seeded passage");
    }

    [Fact]
    public async Task The_export_payload_is_readable_without_a_file_dialog()
    {
        await GotoAsync("library?demo=1");

        var json = await Page.EvaluateAsync<string>("async () => await window.__bmTest.getExportedSave()");

        Assert.Contains("John 3:16", json);
        Assert.Contains("\"schemaVersion\": 1", json);
    }

    [Fact]
    public async Task Writing_a_passage_adds_it_to_the_library()
    {
        await GotoAsync("compose?demo=1");

        await Page.GetByTestId("compose-title").FillAsync("Philippians 4:13");
        await Page.GetByTestId("compose-text").FillAsync("I can do all things through Christ which strengtheneth me.");

        await Assertions.Expect(Page.GetByTestId("compose-word-count")).ToContainTextAsync("10 words");

        await Page.GetByTestId("compose-save").ClickAsync();

        // Saving goes straight to practice for the passage just written.
        await Assertions.Expect(Page.GetByTestId("practice-heading")).ToContainTextAsync("Philippians 4:13");

        // Navigated by clicking rather than reloading: demo mode keeps its library
        // in memory, so a reload would legitimately re-seed and drop this passage.
        await Page.GetByTestId("nav-library").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("passage-card")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task Demo_mode_starts_fresh_on_every_reload()
    {
        await GotoAsync("compose?demo=1");
        await Page.GetByTestId("compose-text").FillAsync("Something temporary.");
        await Page.GetByTestId("compose-save").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("practice-heading")).ToBeVisibleAsync();

        await GotoAsync("library?demo=1");

        // Demo mode is deliberately ephemeral, so a reload is back to the seed.
        await Assertions.Expect(Page.GetByTestId("passage-card")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task A_passage_can_be_deleted()
    {
        await GotoAsync("library?demo=1");

        await Page.GetByTestId("delete-passage").First.ClickAsync();

        await Assertions.Expect(Page.GetByTestId("passage-card")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.GetByTestId("library-message")).ToContainTextAsync("Deleted");
    }

    [Fact]
    public async Task Settings_changes_stick_while_the_session_lasts()
    {
        await GotoAsync("settings?demo=1");
        await Page.GetByTestId("retry-miss").UncheckAsync();

        await Page.GetByTestId("nav-library").ClickAsync();
        await Page.GetByTestId("nav-settings").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("retry-miss")).Not.ToBeCheckedAsync();
    }

    /// <summary>
    /// The real persistence guarantee: outside demo mode the library is written to
    /// localStorage, so it has to survive a full page reload.
    /// </summary>
    [Fact]
    public async Task In_the_real_app_work_survives_a_reload()
    {
        await GotoAsync("compose");
        await Page.GetByTestId("compose-title").FillAsync("Kept across a reload");
        await Page.GetByTestId("compose-text").FillAsync("The LORD is my shepherd; I shall not want.");
        await Page.GetByTestId("compose-save").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("practice-heading")).ToBeVisibleAsync();

        await GotoAsync("library");

        await Assertions.Expect(Page.GetByTestId("passage-card")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.GetByTestId("passage-card")).ToContainTextAsync("Kept across a reload");
    }

    private const string SeededLibraryJson =
        """
        {
          "schemaVersion": 1,
          "exportedUtc": "2026-01-01T09:00:00+00:00",
          "passages": [
            {
              "id": "b0000000-0000-4000-8000-000000000001",
              "title": "Seeded passage",
              "reference": "",
              "translation": "",
              "translationName": "",
              "text": "For God so loved the world",
              "source": "Written",
              "createdUtc": "2026-01-01T09:00:00+00:00",
              "modifiedUtc": "2026-01-01T09:00:00+00:00"
            }
          ],
          "progress": [],
          "settings": {}
        }
        """;
}
