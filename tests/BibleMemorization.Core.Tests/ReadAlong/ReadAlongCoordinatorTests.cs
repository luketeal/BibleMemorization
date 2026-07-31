using BibleMemorization.Core.Model;
using BibleMemorization.Core.ReadAlong;
using BibleMemorization.Core.Speech;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.ReadAlong;

/// <summary>
/// Drives the whole call-and-response flow against the fakes. Delays are collapsed
/// so the state machine is exercised rather than the clock.
/// </summary>
public class ReadAlongCoordinatorTests
{
    private const string Verse = "For God so loved the world";

    private readonly FakeSpeechSynthesizer _synth = new();
    private readonly FakeSpeechRecognizer _mic = new();

    private static AppSettings Settings => AppSettings.Default with
    {
        ReadAlongPauseMs = 1,
        ReadAlongListenTimeoutMs = 50,
        ReadAlongRetryOnMiss = false,
    };

    /// <summary>
    /// Answers each listen window with the next scripted reply. Wired through the
    /// injected delay so a reply lands while the microphone is genuinely open.
    /// </summary>
    private ReadAlongCoordinator Coordinator(params string?[] replies)
    {
        var queue = new Queue<string?>(replies);

        return new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            // The listen timeout is the only delay long enough to answer during.
            if (ms >= 50 && queue.Count > 0)
            {
                var reply = queue.Dequeue();

                if (reply is not null)
                {
                    _mic.Emit(reply);
                    return;
                }
            }

            await Task.Yield();
        });
    }

    private static TokenizedPassage Passage(string text = Verse) => Tokenizer.Tokenize(text);

    [Fact]
    public async Task With_nothing_hidden_the_app_simply_reads_the_passage()
    {
        await using var coordinator = Coordinator();

        await coordinator.RunAsync(Passage(), [], Settings);

        Assert.Equal([Verse], _synth.SpokenText);
        Assert.Equal(ReadAlongPhase.Finished, coordinator.Phase);
    }

    [Fact]
    public async Task The_app_reads_around_a_blank_and_waits_for_the_user()
    {
        await using var coordinator = Coordinator("God");

        await coordinator.RunAsync(Passage(), [1], Settings);

        Assert.Equal(["For", "so loved the world"], _synth.SpokenText);
        Assert.Contains(1, coordinator.Filled);
    }

    /// <summary>
    /// The property the whole mode depends on. A microphone left open during
    /// playback transcribes the app's own voice and scores it as the user's answer.
    /// </summary>
    [Fact]
    public async Task The_microphone_is_never_open_while_the_app_speaks()
    {
        var openWhileSpeaking = false;

        var synth = new FakeSpeechSynthesizer();
        var coordinator = new ReadAlongCoordinator(synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                _mic.Emit("God");
            }

            await Task.Yield();
        });

        coordinator.Changed += () =>
        {
            if (coordinator.Phase == ReadAlongPhase.Speaking && _mic.IsListening)
            {
                openWhileSpeaking = true;
            }
        };

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);
        }

        Assert.False(openWhileSpeaking, "The microphone was open while the app was speaking.");
    }

    [Fact]
    public async Task Speech_arriving_outside_a_listen_window_is_ignored()
    {
        await using var coordinator = Coordinator("God");

        await coordinator.RunAsync(Passage(), [1], Settings);

        // The fake records anything emitted while closed; nothing should have been.
        Assert.Empty(_mic.EmittedWhileClosed);
    }

    [Fact]
    public async Task The_microphone_is_closed_again_once_the_run_ends()
    {
        await using var coordinator = Coordinator("God");

        await coordinator.RunAsync(Passage(), [1], Settings);

        Assert.False(_mic.IsListening);
    }

    [Fact]
    public async Task A_correct_answer_fills_the_blank()
    {
        await using var coordinator = Coordinator("God");

        await coordinator.RunAsync(Passage(), [1], Settings);

        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.True(outcome.Correct);
        Assert.False(outcome.Revealed);
        Assert.Equal("God", outcome.Expected);
    }

    [Fact]
    public async Task Case_and_punctuation_are_forgiven_the_same_as_when_typing()
    {
        await using var coordinator = Coordinator("god.");

        await coordinator.RunAsync(Passage(), [1], Settings);

        Assert.True(Assert.Single(coordinator.Outcomes).Correct);
    }

    [Fact]
    public async Task A_wrong_answer_is_recorded_and_the_word_is_read_out()
    {
        await using var coordinator = Coordinator("dog");

        await coordinator.RunAsync(Passage(), [1], Settings);

        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.False(outcome.Correct);
        Assert.True(outcome.Revealed);
        Assert.Equal("dog", outcome.Heard);

        // The right word is spoken so the user is not left stuck.
        Assert.Contains("God", _synth.SpokenText);
        Assert.DoesNotContain(1, coordinator.Filled);
    }

    [Fact]
    public async Task Silence_past_the_timeout_moves_on_rather_than_hanging()
    {
        await using var coordinator = Coordinator(replies: [null]);

        await coordinator.RunAsync(Passage(), [1], Settings);

        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.False(outcome.Correct);
        Assert.Null(outcome.Heard);
        Assert.Equal(ReadAlongPhase.Finished, coordinator.Phase);
    }

    [Fact]
    public async Task With_retry_on_a_second_chance_is_given()
    {
        await using var coordinator = Coordinator("dog", "God");

        await coordinator.RunAsync(Passage(), [1], Settings with { ReadAlongRetryOnMiss = true });

        // The retry succeeded, so only the successful outcome is recorded.
        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.True(outcome.Correct);
        Assert.Contains(1, coordinator.Filled);
        Assert.Equal(2, _mic.StartCount);
    }

    [Fact]
    public async Task Missing_twice_reveals_the_word()
    {
        await using var coordinator = Coordinator("dog", "cat");

        await coordinator.RunAsync(Passage(), [1], Settings with { ReadAlongRetryOnMiss = true });

        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.False(outcome.Correct);
        Assert.True(outcome.Revealed);
    }

    [Fact]
    public async Task Consecutive_hidden_words_are_answered_in_one_go()
    {
        await using var coordinator = Coordinator("the world");

        await coordinator.RunAsync(Passage(), [4, 5], Settings);

        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.True(outcome.Correct);
        Assert.Contains(4, coordinator.Filled);
        Assert.Contains(5, coordinator.Filled);
    }

    /// <summary>
    /// People run on past a blank into the next phrase. Requiring an exact match
    /// would fail an answer that was actually right.
    /// </summary>
    [Fact]
    public async Task Running_on_past_the_blank_still_counts_as_correct()
    {
        await using var coordinator = Coordinator("God so loved");

        await coordinator.RunAsync(Passage(), [1], Settings);

        Assert.True(Assert.Single(coordinator.Outcomes).Correct);
    }

    [Fact]
    public async Task Answering_out_of_order_does_not_count()
    {
        await using var coordinator = Coordinator("world the");

        await coordinator.RunAsync(Passage(), [4, 5], Settings);

        Assert.False(Assert.Single(coordinator.Outcomes).Correct);
    }

    [Fact]
    public async Task Several_blanks_are_walked_through_in_order()
    {
        await using var coordinator = Coordinator("God", "world");

        await coordinator.RunAsync(Passage(), [1, 5], Settings);

        Assert.Equal(2, coordinator.Outcomes.Count);
        Assert.All(coordinator.Outcomes, o => Assert.True(o.Correct));
        Assert.Equal(["For", "so loved the"], _synth.SpokenText);
    }

    [Fact]
    public async Task Stopping_ends_the_run_and_closes_the_microphone()
    {
        await using var coordinator = Coordinator("God");
        await coordinator.RunAsync(Passage(), [1], Settings);

        await coordinator.StopAsync();

        Assert.Equal(ReadAlongPhase.Idle, coordinator.Phase);
        Assert.False(_mic.IsListening);
        Assert.False(coordinator.IsRunning);
    }

    [Fact]
    public async Task A_recognizer_that_gives_up_does_not_hang_the_run()
    {
        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                // Simulates the browser ending the session on its own.
                _mic.EndSession("network");
            }

            await Task.Yield();
        });

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);

            // Asserted before disposal, which deliberately returns the phase to Idle.
            Assert.Equal(ReadAlongPhase.Finished, coordinator.Phase);
            Assert.Single(coordinator.Outcomes);
        }
    }

    // ---- What the passage view reads off the coordinator ----

    [Fact]
    public async Task Interim_speech_is_exposed_for_display_but_never_scored()
    {
        var seen = new List<string?>();

        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                // An interim guess, then the revised final. Only the final decides.
                _mic.Emit("gawd", isFinal: false);
                _mic.Emit("God", isFinal: true);
            }

            await Task.Yield();
        });

        coordinator.Changed += () =>
        {
            if (coordinator.LiveTranscript is not null)
            {
                seen.Add(coordinator.LiveTranscript);
            }
        };

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);

            Assert.Contains("gawd", seen);
            Assert.True(Assert.Single(coordinator.Outcomes).Correct);
        }
    }

    /// <summary>
    /// Chrome with continuous = true routinely never finalises the tail utterance, so
    /// scoring only finalised text threw away the answer the user had just watched
    /// appear — marked wrong, then read the word they had said correctly.
    /// </summary>
    [Fact]
    public async Task An_answer_the_recognizer_never_finalised_is_still_scored()
    {
        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                // Interim only: no final ever arrives, so the listen window times out.
                _mic.Emit("God", isFinal: false);
            }

            await Task.Yield();
        });

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);

            var outcome = Assert.Single(coordinator.Outcomes);
            Assert.True(outcome.Correct);
            Assert.Equal("God", outcome.Heard);
            Assert.Contains(1, coordinator.Filled);
        }
    }

    [Fact]
    public async Task Finalised_words_are_preferred_over_the_interim_guess()
    {
        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                _mic.Emit("God", isFinal: true);
                // A later interim revision must not displace what was already committed.
                _mic.Emit("dog", isFinal: false);
            }

            await Task.Yield();
        });

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);

            Assert.Equal("God", Assert.Single(coordinator.Outcomes).Heard);
        }
    }

    /// <summary>
    /// A blocked microphone and a user saying nothing look identical from the
    /// coordinator's side unless the recognizer's reason is carried through — and
    /// telling someone to "try again" at a thing that cannot work is worse than useless.
    /// </summary>
    [Fact]
    public async Task A_denied_microphone_is_distinguishable_from_silence()
    {
        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                _mic.EndSession("not-allowed");
            }

            await Task.Yield();
        });

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);

            var outcome = Assert.Single(coordinator.Outcomes);
            Assert.Equal("not-allowed", outcome.Error);
            Assert.Null(outcome.Heard);
        }
    }

    [Fact]
    public async Task Plain_silence_carries_no_error()
    {
        await using var coordinator = Coordinator(replies: [null]);

        await coordinator.RunAsync(Passage(), [1], Settings);

        var outcome = Assert.Single(coordinator.Outcomes);
        Assert.Null(outcome.Error);
        Assert.Null(outcome.Heard);
    }

    [Fact]
    public async Task An_error_on_one_blank_does_not_stick_to_the_next()
    {
        var windows = 0;

        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                windows++;

                if (windows == 1)
                {
                    _mic.EndSession("network");
                }
                else
                {
                    _mic.Emit("world");
                }
            }

            await Task.Yield();
        });

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1, 5], Settings);

            Assert.Equal("network", coordinator.Outcomes[0].Error);
            Assert.Null(coordinator.Outcomes[1].Error);
        }
    }

    [Fact]
    public async Task The_live_transcript_is_cleared_once_the_window_closes()
    {
        await using var coordinator = Coordinator("God");

        await coordinator.RunAsync(Passage(), [1], Settings);

        // Otherwise the previous answer would linger in the next blank.
        Assert.Null(coordinator.LiveTranscript);
    }

    [Fact]
    public async Task Answered_tokens_are_reported_with_their_outcome()
    {
        await using var coordinator = Coordinator("God", "world");

        await coordinator.RunAsync(Passage(), [1, 5], Settings);

        var outcomes = coordinator.TokenOutcomes;
        Assert.True(outcomes[1]);
        Assert.True(outcomes[5]);
    }

    [Fact]
    public async Task A_missed_token_is_reported_as_incorrect()
    {
        await using var coordinator = Coordinator("dog");

        await coordinator.RunAsync(Passage(), [1], Settings);

        Assert.False(coordinator.TokenOutcomes[1]);
    }

    [Fact]
    public async Task Every_token_of_a_multi_word_prompt_is_reported()
    {
        await using var coordinator = Coordinator("the world");

        await coordinator.RunAsync(Passage(), [4, 5], Settings);

        var outcomes = coordinator.TokenOutcomes;
        Assert.True(outcomes[4]);
        Assert.True(outcomes[5]);
    }

    [Fact]
    public async Task The_current_step_reports_which_tokens_it_covers()
    {
        var seenDuringListening = new List<int>();

        var coordinator = new ReadAlongCoordinator(_synth, _mic, async (ms, ct) =>
        {
            if (ms >= 50)
            {
                _mic.Emit("God");
            }

            await Task.Yield();
        });

        coordinator.Changed += () =>
        {
            if (coordinator.Phase == ReadAlongPhase.Listening)
            {
                seenDuringListening.AddRange(coordinator.CurrentTokenIndices);
            }
        };

        await using (coordinator)
        {
            await coordinator.RunAsync(Passage(), [1], Settings);
        }

        // The view highlights these, so they must name the blank being answered.
        Assert.Contains(1, seenDuringListening);
    }
}
