namespace BibleMemorization.Core.Speech;

/// <param name="Text">What the recognizer heard.</param>
/// <param name="IsFinal">
/// False for interim guesses that may still change, true once the recognizer has
/// committed. Interim results are shown live but never scored.
/// </param>
public sealed record SpeechTranscript(string Text, bool IsFinal);

/// <summary>
/// Turns speech into text.
///
/// Behind an interface for two reasons: headless browsers cannot do real speech
/// recognition, so tests would have no way to exercise any of the flows built on
/// it; and Firefox has no implementation at all, so the app needs a supported
/// check rather than a crash.
/// </summary>
public interface ISpeechRecognizer
{
    bool IsSupported { get; }

    bool IsListening { get; }

    event Action<SpeechTranscript>? TranscriptReceived;

    /// <summary>Raised when recognition stops on its own, e.g. on silence or error.</summary>
    event Action<string?>? Ended;

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync();
}
