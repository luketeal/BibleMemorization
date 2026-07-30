// Web Speech API wrappers.
//
// Recognition support is uneven: Chrome and Edge implement it fully, Safari behind
// the webkit prefix, and Firefox not at all. Everything here reports support rather
// than assuming it, so the UI can fall back to typing.

const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

let recognition = null;
let dotNetRef = null;
let stopping = false;

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
    recognition = new SpeechRecognition();
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = language || 'en-US';

    recognition.onresult = (event) => {
        for (let i = event.resultIndex; i < event.results.length; i++) {
            const result = event.results[i];
            const transcript = result[0].transcript.trim();

            if (transcript.length === 0) {
                continue;
            }

            dotNetRef?.invokeMethodAsync('OnTranscript', transcript, result.isFinal);
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

    try {
        recognition.abort();
    } catch {
        // Already stopped.
    }

    recognition.onresult = null;
    recognition.onerror = null;
    recognition.onend = null;
    recognition = null;
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
