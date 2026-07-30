using BibleMemorization.Core.Speech;
using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// Speech recognition through the browser's Web Speech API.
/// Chrome and Edge support it fully, Safari partially, Firefox not at all.
/// </summary>
public sealed class WebSpeechRecognizer : ISpeechRecognizer, IAsyncDisposable
{
    private readonly JsModule _module;
    private readonly DotNetObjectReference<WebSpeechRecognizer> _selfRef;
    private bool? _isSupported;

    public WebSpeechRecognizer(IJSRuntime jsRuntime)
    {
        _module = new JsModule(jsRuntime, "./js/speech.js");
        _selfRef = DotNetObjectReference.Create(this);
    }

    /// <summary>
    /// Reads false until <see cref="ProbeSupportAsync"/> has run, since JS interop
    /// cannot be awaited from a property. Callers probe once on first render.
    /// </summary>
    public bool IsSupported => _isSupported ?? false;

    public bool IsListening { get; private set; }

    public event Action<SpeechTranscript>? TranscriptReceived;

    public event Action<string?>? Ended;

    public async ValueTask<bool> ProbeSupportAsync()
    {
        _isSupported ??= await _module.InvokeAsync<bool>("recognitionSupported");
        return _isSupported.Value;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsListening)
        {
            return;
        }

        var started = await _module.InvokeAsync<bool>("startRecognition", _selfRef, "en-US");
        IsListening = started;

        if (!started)
        {
            Ended?.Invoke("Speech recognition could not start.");
        }
    }

    public async Task StopAsync()
    {
        if (!IsListening)
        {
            return;
        }

        IsListening = false;
        await _module.InvokeVoidAsync("stopRecognition");
    }

    [JSInvokable]
    public void OnTranscript(string text, bool isFinal) =>
        TranscriptReceived?.Invoke(new SpeechTranscript(text, isFinal));

    [JSInvokable]
    public void OnEnded(string? error)
    {
        IsListening = false;
        Ended?.Invoke(error);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is unloading.
        }

        await _module.DisposeAsync();
        _selfRef.Dispose();
    }
}
