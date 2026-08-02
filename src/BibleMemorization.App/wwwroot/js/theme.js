// Appearance preference, driven from the Settings page. The resolution logic itself
// lives in the inline script in index.html, because it has to run before the first
// paint — this module drives that same logic afterwards rather than duplicating it.

const boot = () => window.__bmTheme;

/** The theme currently applied, e.g. 'light' or 'dark'. */
export function current() {
    return document.documentElement.getAttribute('data-theme');
}

/** The stored choice, or null when following the device. */
export function get() {
    try {
        return window.localStorage.getItem(boot().KEY);
    } catch {
        return null;
    }
}

/** Stores an explicit choice and applies it. Passing null goes back to following the device. */
export function set(theme) {
    try {
        if (theme === null) {
            window.localStorage.removeItem(boot().KEY);
        } else {
            window.localStorage.setItem(boot().KEY, theme);
        }
    } catch {
        // Storage blocked. The choice still applies for this session.
    }

    // system(), not resolve(): removeItem above can throw while getItem still
    // succeeds — a quota error, or a partially blocked store — and resolve() would
    // then read the stale key and re-apply the theme just turned off. system() reads
    // the device and cannot go stale.
    boot().apply(theme ?? boot().system());
    return current();
}
