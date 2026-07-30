namespace BibleMemorization.Core.Speech;

/// <summary>
/// Records what would have been spoken instead of making a sound.
///
/// With <see cref="AutoComplete"/> off, an utterance stays pending until the test
/// completes it, which is how read-along timing can be driven step by step rather
/// than raced against.
/// </summary>
public sealed class FakeSpeechSynthesizer : ISpeechSynthesizer
{
    private TaskCompletionSource? _pending;

    public bool IsSupported { get; set; } = true;

    /// <summary>When true, speech finishes the moment it starts.</summary>
    public bool AutoComplete { get; set; } = true;

    /// <summary>Everything spoken so far, in order.</summary>
    public List<string> SpokenText { get; } = [];

    public List<SpeechOptions> SpokenWith { get; } = [];

    public int CancelCount { get; private set; }

    /// <summary>True while an utterance is in flight.</summary>
    public bool IsSpeaking => _pending is { Task.IsCompleted: false };

    public Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken ct = default)
    {
        SpokenText.Add(text);
        SpokenWith.Add(options ?? SpeechOptions.Default);

        if (AutoComplete)
        {
            return Task.CompletedTask;
        }

        _pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pending.Task;
    }

    /// <summary>Finishes the utterance currently in flight.</summary>
    public void CompleteSpeaking()
    {
        _pending?.TrySetResult();
        _pending = null;
    }

    public Task CancelAsync()
    {
        CancelCount++;
        _pending?.TrySetResult();
        _pending = null;
        return Task.CompletedTask;
    }
}
