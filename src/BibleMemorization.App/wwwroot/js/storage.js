// localStorage access and file download/upload helpers.

export function get(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        // Private browsing modes can throw on access rather than just returning null.
        return null;
    }
}

export function set(key, value) {
    try {
        window.localStorage.setItem(key, value);
        return true;
    } catch {
        // Quota exceeded, or storage blocked. The caller surfaces this to the user,
        // whose data is still safe in memory and exportable to a file.
        return false;
    }
}

export function remove(key) {
    try {
        window.localStorage.removeItem(key);
    } catch {
        // Nothing useful to do.
    }
}

export function isAvailable() {
    try {
        const probe = '__bm_probe__';
        window.localStorage.setItem(probe, '1');
        window.localStorage.removeItem(probe);
        return true;
    } catch {
        return false;
    }
}

// Downloads text as a file. Browsers only allow this from a user gesture, which is
// why the save-file provider declares RequiresUserGesture.
export function downloadText(fileName, text, mimeType) {
    const blob = new Blob([text], { type: mimeType || 'application/json' });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);

    // Revoking immediately can cancel the download in some browsers.
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}
