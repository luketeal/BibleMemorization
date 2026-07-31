// Appearance preference. The resolution logic itself lives in the inline script in
// index.html, because it has to run before the first paint — this module drives that
// same logic afterwards rather than duplicating it.

const boot = () => window.__bmTheme;

/** The theme currently applied, e.g. 'blue' or 'dark'. */
export function current() {
    return document.documentElement.getAttribute('data-theme');
}

/** The stored choice, or null when following the system. */
export function get() {
    try {
        return window.localStorage.getItem(boot().KEY);
    } catch {
        return null;
    }
}

/** Stores an explicit choice and applies it. Passing null goes back to following the system. */
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

    boot().apply(theme ?? boot().resolve());
    return current();
}

// Follow the system when it changes, but only while no explicit choice is stored —
// otherwise the user's own setting would be overridden the moment their machine
// switched to night mode.
try {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        if (get() === null) {
            boot().apply(boot().resolve());
        }
    });
} catch {
    // No matchMedia, or no listener support. The theme is simply fixed for the session.
}
