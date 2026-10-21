# Contributing to Soundboard Lite

## Before you start

This is a young project (still pre-1.0, no releases cut yet) with a fairly deliberate architecture —
skim `README.md`'s "Architecture notes" section first. For anything beyond a small fix, open an issue
to discuss the approach before writing code; it'll save you a rewrite if the direction doesn't fit.

## Development setup

```
dotnet build
dotnet test
dotnet run --project src/SoundboardLite.App/SoundboardLite.App.csproj
```

All five `src/` projects build warnings-as-errors for nullable-reference violations
(`Directory.Build.props`) — fix warnings, don't suppress them, unless you have a specific reason
(and a comment explaining it).

## Code style

- No comments explaining *what* code does — names should do that. Comments are for *why*: a
  non-obvious constraint, a workaround for a specific library quirk, an invariant that would surprise
  a reader. If you hit one of those, say so — several files in this repo have comments documenting
  exactly which API assumption turned out to be wrong and why (NAudio 3.x's `Span<float>`-based API,
  `SendInput`'s real struct size, etc.) — that kind of note has saved real time for later contributors.
- Match the existing layering: `Core` has no Windows/NAudio dependency and shouldn't gain one;
  Windows-specific code lives in `Audio`/`App`/`Api`.
- `IPlaybackCoordinator` is the one place trigger/stop logic lives — if you're adding a new way to
  trigger a clip (another protocol, another UI surface), call through it rather than duplicating its
  behavior.

## Testing expectations

- New logic that can be unit-tested without a live NAudio/WASAPI session should be — see
  `tests/SoundboardLite.Core.Tests` and `tests/SoundboardLite.Api.Tests` for examples of testing pure
  logic in isolation (e.g. `TokenAuthTests` exercises ASP.NET Core middleware directly via
  `DefaultHttpContext`, no test server needed).
- For anything UI-, audio-, or OS-integration-facing, a passing test suite is necessary but not
  sufficient — actually run the app and exercise the change before calling it done. This codebase has
  caught real bugs (a silently-broken WPF data binding, a wrong P/Invoke struct size) that built clean
  and passed every test, and only showed up when someone actually ran it.
- A new test project needs to be added explicitly to `SoundboardLite.slnx`, or `dotnet build`/
  `dotnet test` at the repo root won't pick it up.

## Stream Deck plugin

`streamdeck-plugin/` is a separate npm/TypeScript project, not part of the `.slnx`. See its own
README for build instructions and what has/hasn't been verified against real Stream Deck
hardware/software — most of it has, as of the dated entry in that README; check there before assuming
a gap still exists.

## Releasing

Push a tag matching `v*` (e.g. `v1.0.0`) to trigger `.github/workflows/release.yml`, which builds and
tests the .NET solution, publishes the self-contained single-file app, builds and packages the Stream
Deck plugin (`streamdeck pack`), and creates a GitHub Release with both files attached — a
`SoundboardLite-vX.Y.Z-win-x64.zip` and a `com.soundboardlite.streamdeck-vA.B.C.streamDeckPlugin`. The
workflow also accepts manual `workflow_dispatch` runs (give it a version) for a dry-run build without
cutting a release — useful for testing the pipeline itself.

**The plugin's version is tracked independently of the app's/the release tag's** — bump
`streamdeck-plugin/com.soundboardlite.streamdeck.sdPlugin/manifest.json`'s `"Version"` field by hand,
in whatever commit actually changes something under `streamdeck-plugin/`, and leave it alone in
releases that don't. The release workflow packages whatever that file currently says rather than
stamping it from the tag — a release can bump the app without the plugin changing, and the app's
in-app update check (`SoundboardLite.App.Services.UpdateCheckService`) relies on that distinction to
avoid nagging about a "plugin update" that doesn't actually exist. The manifest's schema requires a
4-part version (`{major}.{minor}.{patch}.{build}`); the release workflow truncates it to 3 parts for
the asset filename the update check parses.

## Pull requests

- Keep PRs focused — one change, one PR.
- Describe *why*, not just *what*, in the PR description.
- If your change touches `IPlaybackCoordinator`, `ClipEditSettings`, or the embedded API's contract
  (`SoundboardLite.Api`), call that out explicitly — those are the three places other code (including
  the Stream Deck plugin) depends on staying stable.
