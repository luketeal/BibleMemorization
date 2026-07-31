using BibleMemorization.Core.Model;
using BibleMemorization.Core.Speech;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.ReadAlong;

public enum ReadAlongPhase
{
    Idle,
    Speaking,
    Pausing,
    Listening,
    Finished,
}

/// <summary>What happened at one listen step.</summary>
/// <param name="Error">
/// Set when the recognizer itself failed, e.g. a denied microphone. Without it a
/// blocked mic is indistinguishable from saying nothing, and the user is told to
/// try again at a thing that cannot work.
/// </param>
public sealed record ListenOutcome(
    IReadOnlyList<int> TokenIndices,
    string Expected,
    string? Heard,
    bool Correct,
    bool Revealed,
    string? Error = null);

/// <summary>
/// Runs a guided read-along: the app reads the words still showing, pauses, and
/// listens for the user to supply the missing ones.
///
/// The single rule everything else serves: the microphone is closed whenever the
/// app is speaking. An open mic during playback transcribes the app's own voice and
/// scores it as the user's answer, which would make the whole mode useless.
/// </summary>
public sealed class ReadAlongCoordinator : IAsyncDisposable
{
    private readonly ISpeechSynthesizer _synthesizer;
    private readonly ISpeechRecognizer _recognizer;
    private readonly Func<int, CancellationToken, Task> _delay;

    private CancellationTokenSource? _run;
    private TaskCompletionSource<string?>? _listening;
    private string _heardSoFar = string.Empty;
    private string? _lastError;

    public ReadAlongCoordinator(
        ISpeechSynthesizer synthesizer,
        ISpeechRecognizer recognizer,
        Func<int, CancellationToken, Task>? delay = null)
    {
        _synthesizer = synthesizer;
        _recognizer = recognizer;

        // Injectable so tests do not have to wait out real pauses.
        _delay = delay ?? ((ms, ct) => Task.Delay(ms, ct));

        _recognizer.TranscriptReceived += OnTranscript;
        _recognizer.Ended += OnRecognizerEnded;
    }

    public ReadAlongPhase Phase { get; private set; } = ReadAlongPhase.Idle;

    /// <summary>Index into the plan of the step currently being handled.</summary>
    public int CurrentStepIndex { get; private set; } = -1;

    public IReadOnlyList<ReadAlongStep> Plan { get; private set; } = [];

    public List<ListenOutcome> Outcomes { get; } = [];

    /// <summary>Token indices the user has supplied correctly this run.</summary>
    public HashSet<int> Filled { get; } = [];

    /// <summary>
    /// What the recognizer is hearing right now, interim guesses included, so the
    /// blank being answered can show the words as they are spoken. Null whenever the
    /// microphone is closed.
    /// </summary>
    public string? LiveTranscript { get; private set; }

    /// <summary>Token indices covered by the step currently in flight.</summary>
    public IReadOnlyList<int> CurrentTokenIndices =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Plan.Count
            ? Plan[CurrentStepIndex].TokenIndices
            : [];

    /// <summary>How each answered token turned out, for colouring the passage.</summary>
    public IReadOnlyDictionary<int, bool> TokenOutcomes
    {
        get
        {
            var map = new Dictionary<int, bool>();

            foreach (var outcome in Outcomes)
            {
                foreach (var index in outcome.TokenIndices)
                {
                    map[index] = outcome.Correct;
                }
            }

            return map;
        }
    }

    /// <summary>Raised whenever the phase or progress changes, so the UI can redraw.</summary>
    public event Action? Changed;

    public bool IsRunning => Phase is not (ReadAlongPhase.Idle or ReadAlongPhase.Finished);

    public async Task RunAsync(
        TokenizedPassage passage,
        IReadOnlyCollection<int> hiddenTokenIndices,
        AppSettings settings,
        CancellationToken ct = default)
    {
        await StopAsync();

        Plan = ReadAlongPlanner.Plan(passage, hiddenTokenIndices);
        Outcomes.Clear();
        Filled.Clear();
        CurrentStepIndex = -1;

        _run = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _run.Token;

        try
        {
            for (var i = 0; i < Plan.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                CurrentStepIndex = i;

                if (Plan[i] is SpeakStep speak)
                {
                    await SpeakAsync(speak, settings, token);
                }
                else if (Plan[i] is ListenStep listen)
                {
                    await ListenAsync(listen, settings, token);
                }
            }

            SetPhase(ReadAlongPhase.Finished);
        }
        catch (OperationCanceledException)
        {
            SetPhase(ReadAlongPhase.Idle);
        }
        finally
        {
            await SafeStopListeningAsync();
        }
    }

    private async Task SpeakAsync(SpeakStep step, AppSettings settings, CancellationToken ct)
    {
        // Belt and braces: never speak while the mic is open, even if a previous
        // step exited unusually.
        await SafeStopListeningAsync();

        SetPhase(ReadAlongPhase.Speaking);

        await _synthesizer.SpeakAsync(
            step.Text,
            new SpeechOptions { Rate = settings.SpeechRate, VoiceName = settings.VoiceName },
            ct);
    }

    private async Task ListenAsync(ListenStep step, AppSettings settings, CancellationToken ct)
    {
        var attempts = settings.ReadAlongRetryOnMiss ? 2 : 1;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            SetPhase(ReadAlongPhase.Pausing);

            // The gap between speech ending and the mic opening. It gives the user a
            // beat to start, and keeps any tail of the app's own audio out of the
            // transcript.
            await _delay(settings.ReadAlongPauseMs, ct);

            var heard = await CaptureAsync(settings.ReadAlongListenTimeoutMs, ct);
            var correct = Matches(step, heard);

            if (correct)
            {
                foreach (var index in step.TokenIndices)
                {
                    Filled.Add(index);
                }

                Outcomes.Add(new ListenOutcome(step.TokenIndices, step.ExpectedText, heard, true, false, _lastError));
                Changed?.Invoke();
                return;
            }

            if (attempt < attempts)
            {
                continue;
            }

            // Out of attempts: say the words so the user hears the right answer
            // rather than being left stuck.
            Outcomes.Add(new ListenOutcome(step.TokenIndices, step.ExpectedText, heard, false, true, _lastError));
            Changed?.Invoke();

            SetPhase(ReadAlongPhase.Speaking);
            await _synthesizer.SpeakAsync(
                step.ExpectedText,
                new SpeechOptions { Rate = settings.SpeechRate, VoiceName = settings.VoiceName },
                ct);
        }
    }

    /// <summary>
    /// Opens the microphone, waits for speech or the timeout, then closes it again.
    /// </summary>
    private async Task<string?> CaptureAsync(int timeoutMs, CancellationToken ct)
    {
        _heardSoFar = string.Empty;
        _lastError = null;

        // Cleared per window, so the previous blank's answer never lingers in the next.
        LiveTranscript = null;
        _listening = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetPhase(ReadAlongPhase.Listening);
        await _recognizer.StartAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            var timer = _delay(timeoutMs, timeout.Token);
            var finished = await Task.WhenAny(_listening.Task, timer);

            if (finished == _listening.Task)
            {
                await timeout.CancelAsync();

                // Observed so a cancelled timer never surfaces as an unobserved fault.
                _ = timer.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);

                return await _listening.Task;
            }

            // Whatever arrived is still worth scoring, including text the recognizer
            // never finalised. Chrome with continuous = true routinely leaves the tail
            // utterance interim, and discarding it marks a correct answer wrong right
            // after showing the user their own words.
            return BestHeard();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return BestHeard();
        }
        finally
        {
            _listening = null;
            LiveTranscript = null;
            await SafeStopListeningAsync();
        }
    }

    /// <summary>
    /// Judged with the same normalizer the typed paths use, so a spoken answer is
    /// not held to a stricter standard than a written one.
    /// </summary>
    private static bool Matches(ListenStep step, string? heard)
    {
        if (string.IsNullOrWhiteSpace(heard))
        {
            return false;
        }

        var spoken = TextNormalizer.NormalizeWords(heard);
        var expected = step.ExpectedWords.Select(TextNormalizer.NormalizeWord).Where(w => w.Length > 0).ToArray();

        if (expected.Length == 0)
        {
            return false;
        }

        // The expected words must appear in order. Extra words around them are fine:
        // people naturally run on past a blank into the next phrase.
        var position = 0;

        foreach (var word in spoken)
        {
            if (position < expected.Length && word == expected[position])
            {
                position++;
            }
        }

        return position == expected.Length;
    }

    private void OnTranscript(SpeechTranscript transcript)
    {
        if (Phase != ReadAlongPhase.Listening)
        {
            // Anything arriving outside a listen window is not an answer. Dropping it
            // is what stops the app's own speech being scored as the user's.
            return;
        }

        // Interim guesses are shown but never scored: the recognizer revises them as
        // it goes, so committing one would be judging a half-finished sentence.
        if (!transcript.IsFinal)
        {
            LiveTranscript = string.IsNullOrEmpty(_heardSoFar)
                ? transcript.Text
                : $"{_heardSoFar} {transcript.Text}";

            Changed?.Invoke();
            return;
        }

        _heardSoFar = string.IsNullOrEmpty(_heardSoFar)
            ? transcript.Text
            : $"{_heardSoFar} {transcript.Text}";

        LiveTranscript = _heardSoFar;
        Changed?.Invoke();

        _listening?.TrySetResult(_heardSoFar);
    }

    private void OnRecognizerEnded(string? error)
    {
        if (Phase != ReadAlongPhase.Listening)
        {
            return;
        }

        _lastError = error;
        _listening?.TrySetResult(BestHeard());
    }

    /// <summary>
    /// The best text available for scoring: committed words if there are any,
    /// otherwise whatever the recognizer was still revising. The interim text is what
    /// the user has been watching, so it is the answer they believe they gave.
    /// </summary>
    private string? BestHeard()
    {
        if (!string.IsNullOrWhiteSpace(_heardSoFar))
        {
            return _heardSoFar;
        }

        return string.IsNullOrWhiteSpace(LiveTranscript) ? null : LiveTranscript;
    }

    public async Task StopAsync()
    {
        if (_run is not null)
        {
            await _run.CancelAsync();
            _run.Dispose();
            _run = null;
        }

        await _synthesizer.CancelAsync();
        await SafeStopListeningAsync();

        SetPhase(ReadAlongPhase.Idle);
    }

    private async Task SafeStopListeningAsync()
    {
        if (_recognizer.IsListening)
        {
            await _recognizer.StopAsync();
        }
    }

    private void SetPhase(ReadAlongPhase phase)
    {
        Phase = phase;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _recognizer.TranscriptReceived -= OnTranscript;
        _recognizer.Ended -= OnRecognizerEnded;

        await StopAsync();
    }
}
