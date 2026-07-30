namespace BibleMemorization.Core.Model;

/// <summary>User preferences, carried in the save file alongside the passages.</summary>
public sealed record AppSettings
{
    /// <summary>Translation preselected on the import page.</summary>
    public string DefaultTranslation { get; init; } = "eng_kjv";

    public string DefaultTechniqueId { get; init; } = "vanishing-text";

    public InputMode DefaultInputMode { get; init; } = InputMode.Typed;

    /// <summary>Playback rate for spoken passages, where 1.0 is normal speed.</summary>
    public double SpeechRate { get; init; } = 1.0;

    /// <summary>Preferred voice name for playback; empty means the browser default.</summary>
    public string VoiceName { get; init; } = string.Empty;

    /// <summary>
    /// Silence between the app finishing a phrase and listening for the user, in
    /// milliseconds. Long enough to breathe, short enough to keep the rhythm.
    /// </summary>
    public int ReadAlongPauseMs { get; init; } = 600;

    /// <summary>How long to wait for the user to speak before moving on.</summary>
    public int ReadAlongListenTimeoutMs { get; init; } = 6_000;

    /// <summary>Whether a missed word is re-prompted once before being revealed.</summary>
    public bool ReadAlongRetryOnMiss { get; init; } = true;

    public static AppSettings Default { get; } = new();
}
