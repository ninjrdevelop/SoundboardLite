# Soundboard Lite — Stream Deck plugin

Triggers, stops, and panic-stops Soundboard Lite clips from physical Stream Deck keys, via
Soundboard Lite's embedded local API (`SoundboardLite.Api`, started automatically alongside the WPF
app). See `C:\Users\Craig\.claude\plans\i-want-to-design-jazzy-lagoon.md` for the full API contract
and `HANDOVER.md` at the repo root for overall project status.

Not part of the `.slnx` — this is a sibling Node/TypeScript project, per the plan.

## Structure

```
streamdeck-plugin/
├── com.soundboardlite.streamdeck.sdPlugin/   # what actually ships/loads into Stream Deck
│   ├── bin/                                   # compiled output (gitignored, `npm run build`)
│   ├── imgs/                                  # icons — currently 1x1 placeholders, see below
│   ├── ui/                                     # property inspector HTML + shared PI socket boilerplate
│   └── manifest.json
├── src/
│   ├── actions/
│   │   ├── trigger-clip.ts    # one key = trigger one clip
│   │   ├── stop-clip.ts       # one key = stop one specific clip
│   │   └── stop-all.ts        # panic button — stops everything
│   ├── app-connection.ts      # session-file discovery + REST/WebSocket client for the embedded API
│   └── plugin.ts              # entry point; registers actions, answers the PI's board/clip requests
├── package.json / tsconfig.json
```

## Build

```
npm install
npm run build      # tsc -> com.soundboardlite.streamdeck.sdPlugin/bin/
npm run watch       # same, but watches
```

## Verification performed

**Updated 2026-10-21: real Stream Deck hardware and the Stream Deck desktop app (v7.5.1.22901) became
available and the plugin was actually run on them.** Everything below that was previously
"could not be verified" has now been checked for real; see the dated entry at the bottom of this
section for what changed and what's still outstanding.

What was verified, by actually running it against the real Soundboard Lite app:
- `manifest.json` validated against Elgato's real published JSON Schema
  (`@elgato/schemas/streamdeck/plugins/manifest.json`) using `ajv` — structurally correct, not just
  "looks right".
- `npm run build` (`tsc`) compiles clean with `strict: true` — no type errors, including the
  `JsonObject`-constraint issues the Stream Deck SDK's generics enforce (settings/DTOs must satisfy
  `{ [key: string]: JsonValue }`).
- **`src/app-connection.ts`'s actual integration logic** (the part that matters most, and the part
  that's independent of anything Stream Deck-specific) was exercised directly against the real
  running app: `listBoards()`/`listClips()` found the real seeded board/clip, `triggerClip()`/
  `stopClip()` succeeded and produced audible playback, and the `/api/events` WebSocket subscription
  received real `clipTriggered`/`clipStopped` events matching the triggered clip's id.
- **App-restart recovery**: kept a single long-running Node process connected, restarted the .NET app
  externally (new port, new token, same `api-session.json` path), and confirmed the next API call
  transparently re-read the session file and succeeded — the "a 401/connection-refused triggers a
  re-read" behaviour described in the plan actually works, not just reads like it should on paper.

### 2026-10-21 — real hardware/software pass

- **Found and fixed a real startup crash**: `manifest.json` declared `"Software.MinimumVersion": "6.9"`.
  The installed Stream Deck app is 7.5.1. `@elgato/streamdeck`'s `connect()` unconditionally validates
  the manifest's declared minimum against its own default `onDidReceiveSettings`/
  `onDidReceiveGlobalSettings` handling and throws `ERR_NOT_SUPPORTED` if it's below `7.1` — even though
  this plugin never uses those handlers. This crashed the plugin process on every launch (confirmed via
  `<plugin>/logs/com.soundboardlite.streamdeck.N.log` and Stream Deck's own crash-loop retries in its
  main log) until the manifest was bumped to `"7.1"`. This is exactly the kind of bug that only running
  against real software catches — `ajv` schema validation and `tsc` both passed on the old value.
- **Property inspector wire protocol (`ui/pi-common.js`) works**: confirmed via Stream Deck's own log
  (`ESDPropertyInspector::createDetailView`/`destroyDetailView` for `trigger-clip` and `stop-all`) and
  by the user directly confirming the board/clip dropdowns populated with the real seeded "test" board
  and its 14 clips.
- **`SingletonAction`/`@action` keyDown wiring works with a real device**: pressing physical keys
  triggered real `POST /api/clips/{id}/trigger` calls. Independently confirmed (not just taking the
  user's word for it) by opening a separate WebSocket client against `/api/events` and watching real
  `clipTriggered`/`clipStopped` pairs arrive as physical keys were pressed.
- **The `setState()` playback-driven state machine works end-to-end**, confirmed in an unexpected way:
  the two States in `manifest.json` both hardcode `"TitleAlignment": "bottom"`. The user manually set a
  key's title alignment to top/middle via the Stream Deck app's own UI, then noticed it dropped back to
  bottom for the duration of playback and reverted once the clip finished. Stream Deck tracks per-key
  title customization **per visual state**, not globally — the manual edit only applied to the idle
  state (state 0), so switching to state 1 (`key-playing`) on a real `clipTriggered` event exposed that
  state's un-customized manifest default. This is not a plugin bug: it's independent visual proof that
  `handlePlaybackEvent` → `instance.setState(1)`/`setState(0)` in `src/actions/trigger-clip.ts` really
  fires in sync with real playback start/stop on real hardware. Worth keeping both states' default
  `TitleAlignment` identical (as they already are) to minimize this surprise for end users who
  customize title position without realizing it's per-state.
- **Icon placeholders are the reason nothing visibly flashes or shows an icon** — confirmed by `cmp`/
  `md5sum` that `key.png` and `key-playing.png` (and every other `.png` under `imgs/`) are
  byte-identical 1×1 placeholders. Audio played and the state machine above demonstrably switched
  states; there was simply nothing visually different between the two states' images to see. Not a new
  finding — `imgs/` placeholder status was already flagged below — but now confirmed as the actual
  cause of a real on-hardware symptom rather than a theoretical gap.
- Node version: this machine's Elgato-bundled runtime for the plugin was Node 20.20.2, matching the
  manifest's `Nodejs.Version: "20"` — ran without incident.

What is **still not verified**:
- Icon rendering with real artwork — every `.png` referenced from `manifest.json` is still a 1×1
  placeholder (just enough to pass manifest validation and let the plugin load, and now confirmed to be
  the reason state changes are invisible on a real device). Replace with real artwork at the documented
  dimensions (plugin icon 256×256/512×512, action list icon 20×20/40×40, key image 72×72/144×144)
  before distributing this to anyone.
- `Stop Clip` and `Stop All (Panic)` actions were not pressed on real hardware this session — only
  `Trigger Clip` was. Their underlying `app-connection.ts` calls (`stopClip`/`stopAll`) were already
  verified directly against the API in an earlier session; only the real-device `keyDown` wiring for
  those two specific actions remains unexercised.

## Installing locally for manual testing

```
npm install -g @elgato/cli
streamdeck link com.soundboardlite.streamdeck.sdPlugin
streamdeck restart com.soundboardlite.streamdeck
```
