namespace BibleMemorization.Core.Speech;

/// <summary>
/// A microphone that hears exactly what a test tells it to.
///
/// It also records when listening started and stopped, which is what lets tests
/// assert the microphone was closed while the app was speaking — the single most
/// important property of guided read-along, since an open mic during playback
/// transcribes the app's own voice.
/// </summary>
public sealed class FakeSpeechRecognizer : ISpeechRecognizer
{
    public bool IsSupported { get; set; } = true;

    public bool IsListening { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    /// <summary>Text emitted while the microphone was closed. Should always be empty.</summary>
    public List<string> EmittedWhileClosed { get; } = [];

    public event Action<SpeechTranscript>? TranscriptReceived;

    public event Action<string?>? Ended;

    public Task StartAsync(CancellationToken ct = default)
    {
        IsListening = true;
        StartCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (IsListening)
        {
            StopCount++;
        }

        IsListening = false;
        return Task.CompletedTask;
    }

    /// <summary>Simulates the user speaking.</summary>
    public void Emit(string text, bool isFinal = true)
    {
        if (!IsListening)
        {
            EmittedWhileClosed.Add(text);
            return;
        }

        TranscriptReceived?.Invoke(new SpeechTranscript(text, isFinal));
    }

    /// <summary>Simulates the recognizer giving up, e.g. on silence or an error.</summary>
    public void EndSession(string? error = null)
    {
        IsListening = false;
        Ended?.Invoke(error);
    }
}
