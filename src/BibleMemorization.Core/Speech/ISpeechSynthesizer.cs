namespace BibleMemorization.Core.Speech;

public sealed record SpeechOptions
{
    /// <summary>1.0 is normal speed.</summary>
    public double Rate { get; init; } = 1.0;

    /// <summary>Empty means the browser's default voice.</summary>
    public string VoiceName { get; init; } = string.Empty;

    public static SpeechOptions Default { get; } = new();
}

/// <summary>
/// Reads text aloud.
///
/// <see cref="SpeakAsync"/> must not resolve until the utterance has actually
/// finished. Guided read-along depends on it: the app has to know speech has ended
/// before it opens the microphone, or it will record its own voice.
/// </summary>
public interface ISpeechSynthesizer
{
    bool IsSupported { get; }

    Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken ct = default);

    Task CancelAsync();
}
