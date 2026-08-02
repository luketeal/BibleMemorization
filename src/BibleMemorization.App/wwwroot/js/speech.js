// Web Speech API wrappers.
//
// Recognition support is uneven: Chrome and Edge implement it fully, Safari behind
// the webkit prefix, and Firefox not at all. Everything here reports support rather
// than assuming it, so the UI can fall back to typing.

const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

let recognition = null;
let dotNetRef = null;
let stopping = false;

// The final text already sent on for this session, and the interim guess last
// shown. Both are what let the wrapper send deltas rather than replaying results
// the app has already been given. Reset per session.
let committed = '';
let lastInterim = '';

function words(text) {
    return text.split(/\s+/).filter((word) => word.length > 0);
}

// Revisions vary in casing and punctuation — "world" becomes "world," once the
// recognizer settles — so words are compared on their letters alone.
function comparable(word) {
    return word.toLowerCase().replace(/[^\p{L}\p{N}']/gu, '');
}

function sharedPrefix(left, right) {
    let i = 0;

    while (i < left.length && i < right.length && comparable(left[i]) === comparable(right[i])) {
        i++;
    }

    return i;
}

/**
 * Folds one result's transcript into the text built so far.
 *
 * Chrome on Android does not deliver results the way the spec describes: it
 * resends the whole utterance every time it grows, flags each of those growing
 * copies as final, and leaves `resultIndex` at 0 so they look new. Appending
 * them verbatim is what turned one recitation into "for, for God, for God, for
 * God so, for God so loved, ...". So a segment that repeats or extends what is
 * already held replaces it, and only genuinely new speech is appended.
 */
function merge(text, segment) {
    const existing = words(text);
    const incoming = words(segment);

    if (incoming.length === 0) {
        return text;
    }

    const shared = sharedPrefix(existing, incoming);

    // A replay of something already held, possibly truncated.
    if (shared === incoming.length && incoming.length <= existing.length) {
        return existing.join(' ');
    }

    // Sharing a start means it is the same utterance, so the newer copy wins:
    // the recognizer revises its own tail ("the word" becomes "the world").
    if (shared > 0) {
        return incoming.join(' ');
    }

    return [...existing, ...incoming].join(' ');
}

export function recognitionSupported() {
    return !!SpeechRecognition;
}

export function synthesisSupported() {
    return typeof window.speechSynthesis !== 'undefined';
}

export function startRecognition(reference, language) {
    if (!SpeechRecognition) {
        return false;
    }

    // Chrome throws InvalidStateError if start() lands before a previous session has
    // finished closing, so tear down any existing instance first.
    stopRecognition();

    dotNetRef = reference;
    committed = '';
    lastInterim = '';
    recognition = new SpeechRecognition();
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = language || 'en-US';

    recognition.onresult = (event) => {
        // Rebuilt from the whole list rather than the slice at `event.resultIndex`,
        // because Android's replayed results arrive with a stale index. Rebuilding
        // is idempotent, so a result seen twice costs nothing.
        let final = '';
        let interim = '';

        for (let i = 0; i < event.results.length; i++) {
            const result = event.results[i];
            const transcript = (result[0]?.transcript ?? '').trim();

            if (transcript.length === 0) {
                continue;
            }

            if (result.isFinal) {
                final = merge(final, transcript);
            } else {
                interim = merge(interim, transcript);
            }
        }

        // Merged rather than assigned: some engines send each event's results on
        // their own instead of accumulating them, so earlier text is not in here.
        const next = merge(committed, final);

        if (next !== committed) {
            const addition = words(next).slice(sharedPrefix(words(committed), words(next))).join(' ');
            committed = next;
            lastInterim = '';

            if (addition.length > 0) {
                dotNetRef?.invokeMethodAsync('OnTranscript', addition, true);
            }
        }

        let pending = words(interim);
        const settled = words(committed);

        // Android's interim text repeats the words it has already settled on. The
        // app appends finals and only displays interims, so leaving that overlap in
        // would show every settled word twice.
        if (settled.length > 0 && sharedPrefix(settled, pending) === settled.length) {
            pending = pending.slice(settled.length);
        }

        const interimText = pending.join(' ');

        if (interimText.length > 0 && interimText !== lastInterim) {
            lastInterim = interimText;
            dotNetRef?.invokeMethodAsync('OnTranscript', interimText, false);
        }
    };

    recognition.onerror = (event) => {
        // "aborted" and "no-speech" are routine, not failures worth surfacing.
        const error = event.error === 'aborted' || event.error === 'no-speech' ? null : event.error;
        dotNetRef?.invokeMethodAsync('OnEnded', error ?? null);
    };

    recognition.onend = () => {
        if (!stopping) {
            dotNetRef?.invokeMethodAsync('OnEnded', null);
        }
        stopping = false;
    };

    try {
        recognition.start();
        return true;
    } catch {
        return false;
    }
}

export function stopRecognition() {
    if (!recognition) {
        return;
    }

    stopping = true;
    committed = '';
    lastInterim = '';

    try {
        recognition.abort();
    } catch {
        // Already stopped.
    }

    recognition.onresult = null;
    recognition.onerror = null;
    recognition.onend = null;
    recognition = null;

    // onend was detached above, so it will never run to clear this itself. A natural
    // end does not null `recognition`, so the next start() would tear that instance
    // down and re-arm the flag — suppressing every later session's end.
    stopping = false;
}

// Resolves when the utterance has actually finished, which is what lets read-along
// wait for silence before opening the microphone.
export function speak(text, rate, voiceName) {
    return new Promise((resolve) => {
        if (!window.speechSynthesis || !text) {
            resolve();
            return;
        }

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.rate = rate || 1.0;

        if (voiceName) {
            const voice = window.speechSynthesis.getVoices().find((v) => v.name === voiceName);
            if (voice) {
                utterance.voice = voice;
            }
        }

        let settled = false;
        const finish = () => {
            if (!settled) {
                settled = true;
                resolve();
            }
        };

        utterance.onend = finish;
        // A failed utterance must still resolve, or read-along would wait for ever.
        utterance.onerror = finish;

        window.speechSynthesis.speak(utterance);
    });
}

export function cancelSpeech() {
    if (window.speechSynthesis) {
        window.speechSynthesis.cancel();
    }
}

export function listVoices() {
    if (!window.speechSynthesis) {
        return [];
    }

    return window.speechSynthesis.getVoices().map((v) => v.name);
}
