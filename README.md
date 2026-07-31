# Bible Memorization

A browser app for learning passages by heart. Words vanish one at a time, and you
fill them back in by typing or by speaking.

**Live:** https://luketeal.github.io/BibleMemorization/
**Try it without setup:** https://luketeal.github.io/BibleMemorization/?demo=1

Blazor WebAssembly, deployed as a static site. There is no server and no account —
everything runs in your browser.

---

## What it does

- **Vanishing text.** Tap any word to make it disappear, leaving a blank the width
  of the word it hides. Tap the blank to bring it back. Bulk controls hide 10% more
  at a time, one word, or everything.
- **First letter.** Every word collapses to its initial — `F G s l t w` — keeping
  the shape of the passage while giving away almost nothing.
- **Four ways to test yourself.** Fill in the blanks, write the whole passage out,
  recite it aloud, or use guided read-along.
- **Guided read-along.** The app reads the words still showing and pauses at each
  blank for you to say the missing ones out loud. You recite *with* it, and it drops
  out where your memory is being tested.
- **Import from the Bible.** Type a reference like `John 3:16-18`, `1 Cor 13` or
  `Psalm 23`, or browse by book and chapter.
- **Write or dictate your own.** Type anything you want to learn, or speak it.
- **Export and import.** Your library saves to a `.save` file you can move between
  browsers and devices.

## Where your data lives

In your browser's local storage — on this device, in this browser only. It does not
sync anywhere, and clearing your browser data will erase it. **Export a `.save` file
if you want to keep it.**

Storage sits behind an interface with four implementations (browser storage, save
file, an in-memory one for demos, and a REST backend). Adding a database or a
sync service later means writing one class, not rewriting the app.

## Browser support

| | Typing | Dictation & speaking |
|---|---|---|
| Chrome, Edge | Yes | Yes |
| Safari 14.1+ (macOS), 14.5+ (iOS) | Yes | Yes |
| Firefox | Yes | **No — Firefox has no speech recognition** |

The app detects support and hides the speech features with an explanation rather
than failing. Everything works by typing in every browser.

Note that Chrome's speech recognition is **cloud-based** — audio is sent to Google's
servers to be transcribed. The app says so wherever the microphone is used.

## Bible text

Passages come from the [Free Use Bible API](https://bible.helloao.org) — no API key,
no rate limits. The default translation is the **King James Version** (public
domain). Copyrighted translations like ESV and NIV require a paid licence and are
not available.

Demo mode bundles three chapters offline: John 3, Psalm 23 and 1 Corinthians 13.

---

## Running it locally

Needs the .NET 10 SDK.

```bash
dotnet run --project src/BibleMemorization.App
```

Then open the URL it prints. Speech needs a secure context, which `localhost`
counts as.

## Tests

```bash
dotnet test tests/BibleMemorization.Core.Tests   # 303 unit tests, fast
dotnet test tests/BibleMemorization.E2E          # 80 browser tests via Playwright
node --test tests/speech-js/speech.test.mjs      # 12 tests for the Web Speech wrapper
```

The Node tests exist because `wwwroot/js/speech.js` is the one file the other two
suites cannot reach: both run against `FakeSpeechRecognizer`, since a headless
browser has no microphone. So the real module is driven in Node against a stand-in
Web Speech API, which is what covers its cross-session state.

The end-to-end tests drive real Chromium headlessly against `?demo=1`, so they need
no network and no microphone. Screenshots land in `artifacts/screenshots/`, and
every test drops one in `artifacts/e2e/` — including failures, which is usually the
quickest way to see what went wrong.

Playwright uses the browser at `PLAYWRIGHT_CHROMIUM_PATH` if set, otherwise
`/opt/pw-browsers/chromium`, otherwise its own. No `playwright install` needed when
a matching Chromium is already present.

## How it is put together

```
src/BibleMemorization.Core/     domain logic, no browser dependencies
src/BibleMemorization.App/      Blazor WebAssembly UI and browser interop
tests/BibleMemorization.Core.Tests/
tests/BibleMemorization.E2E/
tests/speech-js/                the Web Speech wrapper, driven in Node
```

Everything worth testing lives in `Core` and is testable without a browser: the
tokenizer, the scorer, both techniques, the reference parser, the storage layer and
the read-along state machine.

Every dependency on the network, the browser or the clock sits behind an interface
with a real and a fake implementation, chosen by the `?demo=1` flag. That exists
because two things a headless browser cannot do — reach the Bible API and use a
microphone — are exactly the two the app is built around. The same mechanism gives
the deployed site an offline demo.

### Theming

Colours live as custom properties at the top of `wwwroot/css/app.css`, in two tiers:
`--c-*` holds the raw palette and is the only place a literal colour appears, and a
semantic tier gives those colours roles. Rules reference the semantic tier, so a
theme redefines about twenty primitives rather than restating the stylesheet. The
tokens also map onto Bootstrap's own variables, which themes its components too.

Two themes: **slate** (light, the default) and **dark**. Slate is simply the base
values, so it needs no block of its own; dark redefines the primitives under
`:root[data-theme="dark"]`, restating the whole semantic ramp because pale state
fills turn to muck on a dark surface.

The theme is chosen by an inline script in `index.html`, deliberately before the
stylesheets are requested — anything later paints the wrong theme first and swaps it
under the reader. It reads `?theme=`, then `localStorage`, then the operating
system's `prefers-color-scheme`. A theme named in the URL applies to that page only
and is never stored, so a link or a screenshot run cannot overwrite a preference.

Tokens must stay in `app.css`. Blazor's scoped-CSS rewriter appends the scope
attribute to the last compound selector, so a `:root` block in a `.razor.css` would
compile to `:root[b-abc123]` and match nothing — scoped files can read tokens but not
declare them. Splitting them into their own stylesheet would also mean adding an
entry to the cache-bust loop in `deploy.yml`.

To compare themes, run the tour and open the contact sheet it writes:

```bash
E2E_THEME_TOUR=1 dotnet test tests/BibleMemorization.E2E --filter ThemeTour
open artifacts/theme-shots/index.html
```

It photographs seven views under every theme, rows by view and columns by theme. It
asserts nothing and is gated behind the environment variable, so it stays a design
tool rather than CI weight.

### Adding a memory technique

Implement `IMemoryTechnique`, register it in `Program.cs`. The practice view renders
`DisplayToken`s without knowing which technique produced them, so no UI changes are
needed.

### Deployment

Pushing to the working branch runs `.github/workflows/deploy.yml`: tests, then
publish, then GitHub Pages. The workflow rewrites Blazor's `<base href>` to the
repository sub-path and copies `index.html` to `404.html` so client-side routes
survive a refresh.

For Pages to accept a deploy, the repository needs **Settings → Pages → Source =
GitHub Actions**, and the branch must be allowed to deploy to the `github-pages`
environment.
