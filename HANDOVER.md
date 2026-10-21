# Soundboard-Lite — Handover

Written 2026-10-21. **All 5 planned milestones (M1–M5) are complete.** This is a snapshot of a
functionally-finished v1, not living documentation — if you're reading this a while after the date
above, prefer the code, `README.md`, and `CONTRIBUTING.md` over trusting this file; update or delete
it once it's stale. The original architecture/plan is at
`C:\Users\Craig\.claude\plans\i-want-to-design-jazzy-lagoon.md` and is still accurate for how/why
things are built the way they are, even though its own "phased build order" is now fully delivered.

## What this project is

Open-source, lightweight Windows-native soundboard app (a VoiceMod-soundboard-feature alternative,
without the voice-changer bulk). Native .NET/C# WPF. See `README.md` for the user-facing feature list
and build/run instructions — not duplicated here.

Repo: `D:\1Dev\Open Source\soundboard-lite`. **Git-initialized 2026-10-21** (was deliberately left
undone across every prior session until explicitly asked for) — pushed to
`https://github.com/ninjrdevelop/SoundboardLite`, `main` branch. A tag-triggered release pipeline
(`.github/workflows/release.yml`) now builds and attaches both distributables to a GitHub Release on
any `v*` tag push — see `CONTRIBUTING.md`'s "Releasing" section. No release has actually been cut yet
(no tag pushed) — the pipeline's individual steps were rehearsed manually (dotnet publish + zip,
`npm run pack`) but the workflow itself has never run on GitHub Actions.

## Status: M1–M5 all complete and verified (with scoped exceptions noted below)

- **M1** — core playback, board/clip CRUD, click-to-trigger, output device picker.
- **M2** — non-destructive editor (trim/pitch/gain/fade), SoundTouch render pipeline, waveform peaks,
  memory-budgeted LRU decode cache, render-cache GC.
- **M3** — push-to-talk (SendInput, ref-counted), Settings window, per-clip PTT overrides.
- **M4** — embedded local HTTP/WebSocket API (`SoundboardLite.Api`) + a hand-authored
  `streamdeck-plugin/` TypeScript project.
- **M5** — self-contained single-file publish, system tray minimize, output latency setting, clear
  error-state surfacing (missing file / failed render — previously these could crash the app or fail
  silently), `LICENSE`/`README.md`/`CONTRIBUTING.md`/`THIRD_PARTY_NOTICES.md`, GitHub Actions CI for
  both the .NET solution and the npm plugin.

**Known gaps, by design or by environment, not oversight:**
- ~~No actual Stream Deck hardware...~~ **Resolved 2026-10-21**: real hardware and the Stream Deck
  desktop app (v7.5.1.22901) became available and the plugin was linked and run on them. Found and
  fixed one real bug in the process (manifest's `Software.MinimumVersion` was `"6.9"`, but the SDK
  hard-crashes on `connect()` against any installed app below `7.1` regardless of whether the plugin
  uses the version-gated APIs — bumped to `"7.1"`). Property inspector wire protocol, real `keyDown` →
  `triggerClip()` → playback, and the `setState()` playback-driven key state machine were all confirmed
  working against the real device (the last one via title-alignment behavior, not a visible icon swap —
  icons are still 1×1 placeholders). Full details in `streamdeck-plugin/README.md`'s "Verification
  performed" section, dated entry.
- **Important correction, also 2026-10-21**: all of the above was verified via `streamdeck link`'s dev
  symlink, which is **not the same thing** as the real packaged `.streamDeckPlugin`. The actual v1.0.0
  GitHub Release's plugin could not run *at all* for any real user — no `node_modules`, no
  `"type":"module"` context, a silent log-free crash. Fixed by switching to an esbuild bundle; see
  `streamdeck-plugin/README.md`'s "the real v1.0.0 GitHub Release distributable" entry before trusting
  any future "verified on hardware" claim about this plugin — confirm it was tested as a real install,
  not just the symlink.
- Velopack auto-update was an explicit stretch goal in the plan, never required for v1 — not done.
- The published self-contained build was run and functionally verified on *this* dev machine (which
  already has .NET SDKs installed) — not on a genuinely clean VM with zero .NET installed, which the
  plan's M5 verification step calls for. The self-contained/single-file settings mean it *should* be
  machine-independent, but that specific claim is unverified.
- System tray icon *click/double-click* interactions were not automated (native system tray icons are
  notoriously unreliable to drive via UI Automation, even with dedicated testing tools) — the
  underlying window-hide-instead-of-close behavior (the actual risky/novel part) **was** verified: the
  process stays alive and the embedded API keeps running after the titlebar X is clicked, proven via
  both `Get-Process` and the `api-session.json` file persisting.

### M5 deliverables (done this session)

- **Publish config** (`SoundboardLite.App.csproj`): `RuntimeIdentifier=win-x64`,
  `SelfContained=true`, `PublishReadyToRun=true`, `PublishSingleFile=true`, `PublishTrimmed=false`
  (trimming stays off — Native AOT isn't viable for WPF's reflection-based XAML/BAML loading, matching
  the plan). Actually published and ran the output: a 243MB single-file `.exe`, launches, embedded API
  starts (confirmed via `api-session.json`), triggered a real clip via the REST API without crashing.
  Idle memory: **182MB WorkingSet, 109MB PrivateMemory**. The plan's "~150MB idle" target is written
  against PrivateMemory-style figures, not WorkingSet — WorkingSet is inflated by the single-file
  host's mmap'd extraction of the bundled runtime/assemblies, which is shared/evictable OS cache, not
  memory uniquely held by the process. By the more honest measure, this comfortably passes; reported
  both numbers rather than picking the favorable one.
- **Tray minimize** (`MainWindow.xaml.cs`): `System.Windows.Forms.NotifyIcon`, context menu
  (Show/Exit). Closing the window (titlebar X, Alt+F4) cancels the close and `Hide()`s instead;
  **only** the tray menu's "Exit" actually calls `Application.Current.Shutdown()`. See gotcha #1 below
  for the `UseWindowsForms`+WPF type-ambiguity issue this ran into and how it was fixed.
- **Output latency setting**: `IPlaybackCoordinator.SetLowLatencyMode(bool)` toggles WASAPI shared-mode
  latency between the robust default (~100ms) and a low-latency mode (~45ms) per the plan's Memory/
  Perf Guidance section, persisted via `AppSettingsKeys.LowLatencyOutput`, rebuilds the active output
  player immediately (no restart needed) if a device is already selected. Exposed via a new "Output"
  section in the existing `SettingsWindow` (extended, not duplicated — output *device* selection
  already lived on `MainWindow`, this only adds latency).
- **Error-state surfacing** — two real bugs fixed, not just features added:
  - `MainViewModel.TriggerClipAsync` (the delegate wired into every `ClipItemViewModel`'s Play button)
    had **no try/catch** — an exception from `IPlaybackCoordinator.TriggerClipAsync` (e.g. a clip whose
    original file had gone missing from disk) would propagate unhandled onto the UI thread. There was
    also no `DispatcherUnhandledException` handler anywhere in `App.xaml.cs`, meaning **this would
    have silently crashed the entire app** — confirmed this was a real, live bug (not theoretical) by
    actually deleting a clip's backing file and clicking Play before the fix.
  - Added a `DispatcherUnhandledException` handler in `App.xaml.cs` as a last-resort safety net (logs
    + `Handled = true`) — not a substitute for handling expected failures at their source, just a
    backstop for whatever wasn't anticipated.
  - `PlaybackCoordinator.GetOrAcquireAsync` now explicitly checks `File.Exists` on the resolved
    original file path (previously only checked that the DB *record* existed, not the file on disk)
    and throws a clip-name-bearing message instead of a bare GUID-based one.
  - `IRenderQueue.RenderCompleted` (fires on a background thread) is now subscribed to in
    `MainViewModel`'s constructor and surfaces failed renders via `StatusMessage`, marshaled onto the
    UI thread via `Application.Current.Dispatcher.Invoke` — previously a failed render was invisible
    to the user, only ever reaching the Serilog file nobody looks at.
  - **Verified live**: imported a clip, deleted its backing file, clicked Play — got
    `"Couldn't play clip: 'broken-clip' is missing its source file on disk."` in the status bar, app
    stayed fully responsive. (First click attempt actually missed the button by a few pixels and
    produced no error at all, which is itself worth remembering — a `StatusMessage` check that finds
    nothing changed could mean "no bug" or "my own click missed"; re-verify the click landed, e.g. via
    `BoundingRectangle`-derived exact coordinates + `SetForegroundWindow` first, before trusting a
    negative result.)
- **Docs**: `LICENSE` (MIT — not previously decided by the user; picked as the unambiguous default for
  an attribution-only open-source Windows utility with no stated alternative, compatible with every
  dependency in use including SoundTouch.Net's LGPL — flag this pick to the user rather than treating
  it as beyond question), `THIRD_PARTY_NOTICES.md` (the SoundTouch.Net LGPL note pending since M2,
  plus a full dependency table), `README.md`, `CONTRIBUTING.md`.
- **CI**: `.github/workflows/dotnet.yml` (restore/build/test/publish, `windows-latest`) and
  `.github/workflows/streamdeck-plugin.yml` (`npm ci && npm run build`, path-filtered so .NET-only
  changes don't trigger it and vice versa). **Neither has actually run on GitHub Actions** — there's
  no remote (see "still not a git repo" above). Both YAML files were parsed with `js-yaml` to confirm
  they're at least syntactically valid, and `npm ci && npm run build` was run locally to confirm the
  plugin workflow's actual commands work end to end (this doubled as confirming `package-lock.json` is
  in sync with `package.json`, which `npm ci` enforces strictly unlike `npm install`).

## Non-obvious things discovered this session — read before touching tray/WinForms interop code

Carried forward from M1–M4 (still true, not re-verified this session): NAudio 3.3 API shape,
SoundTouch.Net's managed-only nature, WPF-UI 4.3 control shapes, never use `RelativeSource
AncestorType=ItemsControl` from inside an `ItemsControl.ItemTemplate`, the `SendInput` struct-layout
gotcha, and don't guess the `@elgato/streamdeck` SDK's API surface — read the installed `.d.ts` files.

New this session:

1. **`<UseWindowsForms>true</UseWindowsForms>` in a WPF project (`SoundboardLite.App.csproj`, added
   for `NotifyIcon`) injects implicit global usings for `System.Windows.Forms` *and* `System.Drawing`**
   — these collide with WPF's own ambient types across the whole project: `TextBox`, `Application`,
   `Color`, `Size`, `KeyEventArgs`, `MouseEventArgs` all became ambiguous-reference compile errors in
   files that had nothing to do with the tray icon (`KeyCaptureBox.cs`, `WaveformControl.cs`,
   `App.xaml.cs`). Fixed with `<Using Remove="System.Windows.Forms" /><Using Remove="System.Drawing" />`
   in the csproj, then used the fully-qualified `System.Windows.Forms.NotifyIcon` etc. names in the one
   file that actually needs them (`MainWindow.xaml.cs`) instead of relying on the (now-removed)
   implicit usings. If you add more WinForms interop later, follow the same pattern rather than
   re-adding the global usings.
2. **Setting `RuntimeIdentifier` unconditionally in the csproj (needed for `PublishReadyToRun`) changes
   `dotnet build`'s own output path**, not just `dotnet publish`'s — it moves from
   `bin\Debug\net10.0-windows\` to `bin\Debug\net10.0-windows\win-x64\`. Not a bug, just something
   that'll break a hardcoded path in a script/muscle-memory from earlier sessions (several of this
   session's own manual-verification scratch scripts needed updating because of this).
3. **`git diff`/`git status` gave genuinely inconsistent answers within the same shell session**
   (one call reported a real diffstat against a file as if inside a git repo; the very next call, same
   directory, said "not a git repository"). Not chased down — the repo is still not git-initialized as
   far as every other check in this session showed, and no git commands were run that could have
   changed that. If you see this again, verify with `git rev-parse --show-toplevel` before trusting
   either a positive or negative result, and don't assume the repo's git status based on one command.
4. **WorkingSet64 is the wrong metric for "idle RAM" claims on a `PublishSingleFile` build** — it
   includes the mmap'd pages of the bundled runtime/assemblies the single-file host extracts, which
   inflates the number well past what the process actually costs the system. `PrivateMemorySize64` is
   the more honest comparison point. Keep this in mind if anyone later asks "why does Task Manager show
   180MB+ when you said under 150MB" — it's not a regression, it's measuring a different thing.
5. **`streamdeck pack --version X` mutates the *source* `manifest.json` on disk**, not just the
   generated `.streamDeckPlugin` package's copy — it rewrites `Version` and reformats some arrays
   (`Controllers` goes from inline to multi-line) as a side effect. Harmless inside CI (an ephemeral
   checkout), but running it locally for a quick test will leave the tracked manifest dirty —
   `git checkout -- streamdeck-plugin/com.soundboardlite.streamdeck.sdPlugin/manifest.json` after any
   local dry-run that isn't an actual release.

## What's next

The plan's 5 milestones are done. Natural next steps, roughly in order of likely value:

1. ~~Get this in front of a real Stream Deck...~~ **Done 2026-10-21** — see `streamdeck-plugin/README.md`.
2. ~~Decide on `git init` + first commit~~ **Done 2026-10-21** — pushed to
   `https://github.com/ninjrdevelop/SoundboardLite`, release pipeline added.
3. **Cut an actual first release** (`git tag v1.0.0 && git push --tags`) — the release workflow has
   never actually run on GitHub Actions yet, only had its individual steps rehearsed locally.
4. Real icon artwork for the app itself (currently relies on the default .NET WinExe icon via
   `Icon.ExtractAssociatedIcon` for the tray; no custom app icon has been set anywhere). The Stream
   Deck plugin's icons were generated and shipped 2026-10-21 — see `streamdeck-plugin/README.md`.
5. Verify the published build on an actual clean machine/VM (no .NET installed) — the one part of the
   plan's M5 verification checklist this session couldn't do.
6. Velopack auto-update, if desired — explicit stretch goal, was never required for v1.

## Build / test / run / publish commands

```
cd "D:\1Dev\Open Source\soundboard-lite"
dotnet build                                                        # whole solution
dotnet test                                                          # all 57 tests (16 Core, 3 Data, 27 Audio, 11 Api)
dotnet run --project src\SoundboardLite.App\SoundboardLite.App.csproj   # run the app
dotnet publish src\SoundboardLite.App\SoundboardLite.App.csproj -c Release   # self-contained single-file

cd streamdeck-plugin
npm ci
npm run build
```

App data lives at `%LOCALAPPDATA%\SoundboardLite\` (db, originals, cache, peaks, runtime incl.
`api-session.json` while running, logs — all created on first run by `AppPaths`). It's currently
empty/fresh — all test data created across every session was cleaned up afterward.

## Where everything else is

- Approved plan (full architecture, data model, API contract, milestone list):
  `C:\Users\Craig\.claude\plans\i-want-to-design-jazzy-lagoon.md`
- User-facing docs: `README.md`, `CONTRIBUTING.md`, `LICENSE`, `THIRD_PARTY_NOTICES.md` (repo root).
- Stream Deck plugin specifics: `streamdeck-plugin/README.md`.
- This handover: `D:\1Dev\Open Source\soundboard-lite\HANDOVER.md` — a snapshot, not living
  documentation. Now that the milestone plan is fully delivered, consider whether this file has
  outlived its usefulness and the repo's own docs (README, code) are self-explanatory enough to retire
  it, rather than mechanically updating it forever.
