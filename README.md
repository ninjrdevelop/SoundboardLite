# Soundboard Lite

A lightweight, open-source Windows soundboard — the soundboard half of apps like VoiceMod, without
the voice-changer bulk. Native .NET/WPF, not Electron.

## Features

- **Boards of clips** — import audio files (WAV, MP3, M4A, AAC, WMA, FLAC), organize into boards,
  click to trigger. Polyphonic: the same clip can overlap itself, different clips always can.
- **Non-destructive editing** — trim, pitch-shift, gain, and fades, with a waveform editor. The
  original imported file is never modified; edits render to a cache file in the background.
- **Selectable output device** — route playback to any Windows output device, including a virtual
  cable (e.g. VB-Audio) to feed Discord/game voice chat. This is not a virtual microphone driver —
  you still need a separate virtual audio cable if you want to route into another app's mic input.
- **Push-to-talk simulation** — optionally hold a configurable key (global default + per-clip
  overrides) around playback, with configurable begin/end delays, so your Discord/game PTT opens
  automatically when a clip plays.
- **Stream Deck support** — an embedded local HTTP/WebSocket API plus a companion Stream Deck plugin
  (`streamdeck-plugin/`) to trigger clips, stop clips, and panic-stop everything from physical keys.
- **Low-latency option**, system tray minimize, self-contained single-file distribution (no separate
  .NET runtime install required).

## Requirements

- Windows 10 or later (WASAPI-based; this is Windows-only).
- To build: .NET 10 SDK.
- To build the Stream Deck plugin: Node.js 20+.

## Download (no build required)

Grab the latest release from this repo's **[Releases page](../../releases)**. Each release has two
files:

- **`SoundboardLite-vX.Y.Z-win-x64.zip`** — extract anywhere and run `SoundboardLite.App.exe`. It's a
  self-contained single-file build; no separate .NET runtime install needed.
- **`com.soundboardlite.streamdeck.streamDeckPlugin`** — optional, only needed for Stream Deck
  support. Double-click it to install into the Stream Deck app (Soundboard Lite must be running for
  the plugin to connect to it).

## Building and running from source

```
git clone <this repo>
cd soundboard-lite
dotnet build
dotnet run --project src/SoundboardLite.App/SoundboardLite.App.csproj
```

Run the test suite:

```
dotnet test
```

Publish a self-contained single-file build (no separate .NET runtime needed on the target machine):

```
dotnet publish src/SoundboardLite.App/SoundboardLite.App.csproj -c Release
# -> src/SoundboardLite.App/bin/Release/net10.0-windows/win-x64/publish/SoundboardLite.App.exe
```

App data (SQLite library, imported originals, render cache, waveform peaks, logs) lives at
`%LOCALAPPDATA%\SoundboardLite\`.

### Stream Deck plugin

```
cd streamdeck-plugin
npm install
npm run build
```

See `streamdeck-plugin/README.md` for installing it locally and what has/hasn't been verified against
real Stream Deck hardware.

## Project layout

```
src/
├── SoundboardLite.Core/      domain models, abstractions — no Windows/UI dependency
├── SoundboardLite.Data/      SQLite + Dapper repositories, migrations
├── SoundboardLite.Audio/     NAudio playback engine, render pipeline, push-to-talk
├── SoundboardLite.Api/       embedded local HTTP/WebSocket API (for the Stream Deck plugin)
└── SoundboardLite.App/       WPF UI, composition root
tests/                        one test project per src project
streamdeck-plugin/            sibling npm/TypeScript project (not part of the .NET solution)
```

## Architecture notes

- `IPlaybackCoordinator` is the single entry point for triggering/stopping clips — the WPF click
  handlers and the embedded API's REST endpoints both call through it, so behavior (polyphony,
  push-to-talk, render-cache resolution) never diverges by trigger source.
- Edits never touch the original file: `ClipEditSettings` holds trim/pitch/gain/fade, a background
  render queue produces `cache/{renderKey}.wav` (`renderKey = hash(originalHash + settings)`), and
  playback falls back to the untouched original whenever no matching render exists yet.
- The embedded API (`SoundboardLite.Api`) runs as an `IHostedService` inside the same process as the
  WPF app, sharing its actual service instances (not a separate copy) — see
  `ApiHostedService`'s class remarks for why that matters.

## License

MIT — see [LICENSE](LICENSE). Third-party dependency licenses (including one LGPL-2.1 component) are
listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
