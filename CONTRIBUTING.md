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
README for build instructions and — importantly — what has and hasn't been verified against real
Stream Deck hardware/software (as of writing, the core API-integration logic has been, the
property-inspector wire protocol and actual key presses haven't — no hardware was available during
that work). If you have a real Stream Deck, testing that gap and reporting back is genuinely useful.

## Pull requests

- Keep PRs focused — one change, one PR.
- Describe *why*, not just *what*, in the PR description.
- If your change touches `IPlaybackCoordinator`, `ClipEditSettings`, or the embedded API's contract
  (`SoundboardLite.Api`), call that out explicitly — those are the three places other code (including
  the Stream Deck plugin) depends on staying stable.
