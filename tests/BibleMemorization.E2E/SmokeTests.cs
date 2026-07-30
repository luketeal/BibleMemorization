using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

public sealed class SmokeTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task App_shell_boots_and_renders_the_landing_page()
    {
        await GotoAsync();

        await Assertions.Expect(Page.GetByTestId("home-heading")).ToHaveTextAsync("Bible Memorization");
        await Assertions.Expect(Page).ToHaveTitleAsync("Bible Memorization");
    }

    [Fact]
    public async Task Blazor_runtime_loads_without_console_errors()
    {
        var errors = new List<string>();
        Page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };

        await GotoAsync();

        Assert.Empty(errors);
    }
}
