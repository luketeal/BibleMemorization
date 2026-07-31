// Coverage for the one file the C# suites cannot reach.
//
// `speech.js` is the real Web Speech API wrapper. Every C# test — unit and E2E
// alike — runs against `FakeSpeechRecognizer`, because a headless browser has no
// microphone. That left this file's cross-session state entirely unexercised, and
// that is exactly where it was wrong: a manual stop used to suppress the end event
// of every session that followed.
//
// So this drives the actual module in Node against a stand-in Web Speech API.
//
//   node --test tests/speech-js

import { test } from 'node:test';
import assert from 'node:assert/strict';

const MODULE = '../../src/BibleMemorization.App/wwwroot/js/speech.js';

/** Stands in for a browser SpeechRecognition, with hooks to drive it from a test. */
class FakeRecognition {
    static instances = [];

    constructor() {
        FakeRecognition.instances.push(this);
        this.started = false;
        this.aborted = false;
    }

    start() {
        this.started = true;
    }

    abort() {
        this.aborted = true;
    }

    /** The browser closing the session by itself — a pause in speech, or a timeout. */
    endNaturally() {
        this.onend?.();
    }

    fireError(error) {
        this.onerror?.({ error });
    }

    fireResult(transcript, isFinal) {
        this.onresult?.({
            resultIndex: 0,
            results: [{ 0: { transcript }, isFinal, length: 1 }],
        });
    }
}

/** Records what the module would have sent across the JS/.NET boundary. */
function recorder() {
    const calls = [];

    return {
        calls,
        ended: () => calls.filter((c) => c.method === 'OnEnded'),
        transcripts: () => calls.filter((c) => c.method === 'OnTranscript'),
        invokeMethodAsync(method, ...args) {
            calls.push({ method, args });
            return Promise.resolve();
        },
    };
}

/**
 * A fresh copy of the module each time. ESM caches by URL, and the module holds
 * `recognition`/`stopping` at module scope, so without the cache-busting query one
 * test's leftover state would decide the next one's result — the very class of bug
 * this file exists to catch.
 */
let loadCount = 0;

async function loadSpeech({ supported = true } = {}) {
    FakeRecognition.instances = [];

    globalThis.window = {
        SpeechRecognition: supported ? FakeRecognition : undefined,
        speechSynthesis: undefined,
    };

    globalThis.SpeechRecognition = supported ? FakeRecognition : undefined;

    return import(`${MODULE}?load=${loadCount++}`);
}

test('a natural end is reported', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    assert.equal(ref.ended().length, 1);
});

test('a manual stop is not reported as an end', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    speech.stopRecognition();

    // The app asked for the stop, so it does not need telling that it happened.
    assert.equal(ref.ended().length, 0);
    assert.equal(FakeRecognition.instances.at(-1).aborted, true);
});

/**
 * The regression. `stopRecognition` sets a suppression flag and then detaches the
 * very handler that clears it, so the flag used to survive into later sessions —
 * and a natural end never nulls `recognition`, so each new session tore down the
 * lingering instance and re-armed it. Read-along then waited for an end that never
 * came, with a denied microphone looking identical to silence.
 */
test('sessions after a manual stop still report their natural ends', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    // 1: natural end.
    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    // 2: manual stop, deliberately silent.
    speech.startRecognition(ref, 'en-US');
    speech.stopRecognition();

    // 3 and 4: natural ends again, which must be heard.
    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    assert.equal(ref.ended().length, 3, 'sessions 1, 3 and 4 should each report an end');
});

test('a natural end does not suppress the next session', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    assert.equal(ref.ended().length, 2);
});

test('stopping twice in a row is harmless', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    speech.stopRecognition();
    speech.stopRecognition();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).endNaturally();

    assert.equal(ref.ended().length, 1);
});

test('starting again tears the previous session down first', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    const first = FakeRecognition.instances.at(-1);

    speech.startRecognition(ref, 'en-US');

    // Chrome throws InvalidStateError if start() lands on a live session.
    assert.equal(first.aborted, true);
    assert.equal(FakeRecognition.instances.length, 2);
    assert.equal(FakeRecognition.instances.at(-1).started, true);
});

test('a real error is surfaced', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).fireError('not-allowed');

    // A denied microphone has to be distinguishable from the user saying nothing.
    assert.deepEqual(ref.ended().at(-1).args, ['not-allowed']);
});

test('routine errors are reported as ordinary ends', async () => {
    for (const routine of ['aborted', 'no-speech']) {
        const speech = await loadSpeech();
        const ref = recorder();

        speech.startRecognition(ref, 'en-US');
        FakeRecognition.instances.at(-1).fireError(routine);

        assert.deepEqual(ref.ended().at(-1).args, [null], `${routine} should not read as a failure`);
    }
});

test('transcripts carry their final flag through', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).fireResult('  for god so loved  ', false);
    FakeRecognition.instances.at(-1).fireResult('for God so loved the world', true);

    assert.deepEqual(ref.transcripts().map((c) => c.args), [
        ['for god so loved', false],
        ['for God so loved the world', true],
    ]);
});

test('empty transcripts are dropped', async () => {
    const speech = await loadSpeech();
    const ref = recorder();

    speech.startRecognition(ref, 'en-US');
    FakeRecognition.instances.at(-1).fireResult('   ', true);

    assert.equal(ref.transcripts().length, 0);
});

test('an unsupported browser reports so rather than throwing', async () => {
    const speech = await loadSpeech({ supported: false });

    assert.equal(speech.recognitionSupported(), false);
    assert.equal(speech.startRecognition(recorder(), 'en-US'), false);

    // Firefox has no recognition at all; the UI falls back to typing.
    assert.doesNotThrow(() => speech.stopRecognition());
});

test('speak resolves even when the utterance fails', async () => {
    const speech = await loadSpeech();

    let utterance = null;
    globalThis.SpeechSynthesisUtterance = class {
        constructor(text) {
            this.text = text;
            utterance = this;
        }
    };

    globalThis.window.speechSynthesis = {
        speak: () => utterance.onerror(),
        getVoices: () => [],
    };

    // Read-along awaits this before opening the microphone, so a failed utterance
    // that never resolved would hang the whole run.
    await speech.speak('For God so loved the world', 1, null);
});
