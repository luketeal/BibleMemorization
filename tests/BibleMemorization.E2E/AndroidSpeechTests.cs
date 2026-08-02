using BibleMemorization.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace BibleMemorization.E2E;

/// <summary>
/// The one speech test that runs against the real <c>speech.js</c>.
///
/// Every other speech test drives <c>FakeSpeechRecognizer</c> through demo mode,
/// which is what makes them stable — but it also means the wrapper between Blazor
/// and the browser API is never exercised, and that is exactly where Android's
/// results were being read wrong. So this one runs in live mode, where the real
/// WebSpeechRecognizer is registered, and installs a stand-in browser
/// SpeechRecognition that replays results the way Chrome on Android does.
/// </summary>
public sealed class AndroidSpeechTests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private const string Verse =
        "For God so loved the world, that he gave his only begotten Son, that whosoever "
        + "believeth in him should not perish, but have everlasting life.";

    /// <summary>
    /// Installed before any page script, so <c>speech.js</c> picks it up when it
    /// loads — the module reads the constructor once, at import.
    /// </summary>
    private const string AndroidSpeechApi = """
        window.__android = { instance: null };

        window.SpeechRecognition = class {
            constructor() { window.__android.instance = this; }
            start() {}
            stop() {}
            abort() {}
        };

        // Android resends the whole utterance every time it grows, flags every one
        // of those copies as final, and leaves resultIndex at 0.
        window.__android.say = (spoken) => {
            window.__android.instance.onresult({
                resultIndex: 0,
                results: [{ 0: { transcript: spoken }, isFinal: true, length: 1 }],
            });
        };
        """;

    /// <summary>
    /// The regression behind the Samsung bug report: one recitation of John 3:16
    /// came back as "For For God For God For God so ...", every partial guess
    /// stacked on the last, because the wrapper passed each replayed result on as
    /// new speech.
    /// </summary>
    [Fact]
    public async Task Reciting_on_android_leaves_the_passage_said_once()
    {
        await Page.AddInitScriptAsync(AndroidSpeechApi);

        // No ?demo=1: live mode is the only place the real recognizer is registered.
        await GotoAsync("compose");
        await Page.GetByTestId("compose-title").FillAsync("John 3:16");
        await Page.GetByTestId("compose-text").FillAsync(Verse);

        // Saving lands on the practice page for the passage just written.
        await Page.GetByTestId("compose-save").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("passage-view")).ToBeVisibleAsync();

        await Page.GetByTestId("mode-test").ClickAsync();
        await Page.GetByTestId("input-speak").ClickAsync();
        await Page.GetByTestId("speak-toggle").ClickAsync();

        var words = Verse.Split(' ');

        for (var i = 1; i <= words.Length; i++)
        {
            var spoken = string.Join(' ', words[..i]);

            // Sent twice, since a partial repeats until the next word lands.
            await Page.EvaluateAsync("(said) => window.__android.say(said)", spoken);
            await Page.EvaluateAsync("(said) => window.__android.say(said)", spoken);
        }

        await Assertions.Expect(Page.GetByTestId("heard-text")).ToHaveTextAsync(Verse);
    }
}
