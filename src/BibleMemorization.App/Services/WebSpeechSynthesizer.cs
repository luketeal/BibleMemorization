using BibleMemorization.Core.Speech;
using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// Text-to-speech through the browser's speechSynthesis API.
/// </summary>
public sealed class WebSpeechSynthesizer(IJSRuntime jsRuntime) : ISpeechSynthesizer, IAsyncDisposable
{
    private readonly JsModule _module = new(jsRuntime, "./js/speech.js");
    private bool? _isSupported;

    public bool IsSupported => _isSupported ?? false;

    public async ValueTask<bool> ProbeSupportAsync()
    {
        _isSupported ??= await _module.InvokeAsync<bool>("synthesisSupported");
        return _isSupported.Value;
    }

    /// <summary>
    /// Completes only once the utterance has actually finished speaking, which is
    /// what lets guided read-along wait for silence before opening the microphone.
    /// </summary>
    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var settings = options ?? SpeechOptions.Default;
        await _module.InvokeVoidAsync("speak", text, settings.Rate, settings.VoiceName);
    }

    public async Task CancelAsync()
    {
        try
        {
            await _module.InvokeVoidAsync("cancelSpeech");
        }
        catch (JSDisconnectedException)
        {
            // The page is unloading.
        }
    }

    public ValueTask<string[]> ListVoicesAsync() => _module.InvokeAsync<string[]>("listVoices");

    public ValueTask DisposeAsync() => _module.DisposeAsync();
}
