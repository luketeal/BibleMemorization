using BibleMemorization.Core;
using BibleMemorization.Core.Speech;
using BibleMemorization.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// A window.__bmTest object for end-to-end tests to drive the fakes with.
///
/// Registered only in demo mode, so the real app never carries this surface. It
/// exists because the two things a headless browser cannot do — reach the Bible API
/// and use a microphone — are precisely the two the app is built around.
/// </summary>
public sealed class TestHooks(IServiceProvider services)
{
    public static async Task RegisterAsync(IJSRuntime jsRuntime, IServiceProvider services)
    {
        var hooks = new TestHooks(services);
        var reference = DotNetObjectReference.Create(hooks);

        await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/testhooks.js");

        await module.InvokeVoidAsync("register", reference);
    }

    [JSInvokable]
    public void EmitTranscript(string text, bool isFinal) =>
        services.GetRequiredService<FakeSpeechRecognizer>().Emit(text, isFinal);

    [JSInvokable]
    public bool IsListening() =>
        services.GetRequiredService<FakeSpeechRecognizer>().IsListening;

    [JSInvokable]
    public string[] GetSpokenText() =>
        [.. services.GetRequiredService<FakeSpeechSynthesizer>().SpokenText];

    [JSInvokable]
    public void CompleteSpeaking() =>
        services.GetRequiredService<FakeSpeechSynthesizer>().CompleteSpeaking();

    [JSInvokable]
    public async Task SeedLibrary(string json)
    {
        var snapshot = SnapshotSerializer.Deserialize(json);
        await services.GetRequiredService<LibraryService>().ImportAsync(snapshot, ImportMode.Replace);
    }

    [JSInvokable]
    public string GetExportedSave() =>
        SnapshotSerializer.Serialize(services.GetRequiredService<LibraryService>().ToSnapshot());

    [JSInvokable]
    public void SetClock(string iso)
    {
        if (services.GetRequiredService<IClock>() is FixedClock clock
            && DateTimeOffset.TryParse(iso, out var parsed))
        {
            clock.Set(parsed);
        }
    }
}
