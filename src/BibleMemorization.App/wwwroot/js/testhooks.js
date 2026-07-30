// Registered only in demo mode. Gives end-to-end tests a way to drive the fakes:
// speak into the microphone, freeze the clock, read what was exported.

export function register(dotNetRef) {
    window.__bmTest = {
        ready: true,

        // Simulates the user speaking. isFinal false sends an interim guess.
        emitTranscript: (text, isFinal) =>
            dotNetRef.invokeMethodAsync('EmitTranscript', text, isFinal ?? true),

        // Replaces the library wholesale, skipping the UI needed to build one.
        seedLibrary: (json) => dotNetRef.invokeMethodAsync('SeedLibrary', json),

        // The .save payload, without going near a download or a file dialog.
        getExportedSave: () => dotNetRef.invokeMethodAsync('GetExportedSave'),

        // Everything the synthesizer has been asked to say, in order.
        getSpokenText: () => dotNetRef.invokeMethodAsync('GetSpokenText'),

        // Finishes an utterance that is deliberately being held open.
        completeSpeaking: () => dotNetRef.invokeMethodAsync('CompleteSpeaking'),

        // True while the fake microphone is open. Read-along depends on this being
        // false whenever the app is speaking.
        isListening: () => dotNetRef.invokeMethodAsync('IsListening'),

        setClock: (iso) => dotNetRef.invokeMethodAsync('SetClock', iso),
    };
}
