import streamDeck, {
    action,
    DidReceiveSettingsEvent,
    KeyDownEvent,
    SingletonAction,
    TitleParametersDidChangeEvent,
    WillAppearEvent,
} from "@elgato/streamdeck";
import type { JsonObject } from "@elgato/utils";
import { onPlaybackEvent, triggerClip, type PlaybackEvent } from "../app-connection.js";

/** Persisted per-key settings, written by the property inspector (ui/trigger-clip.html). */
export type TriggerClipSettings = JsonObject & {
    boardId?: string;
    boardName?: string;
    clipId?: string;
    clipName?: string;
};

/**
 * One Stream Deck key = one clip. Pressing it calls the same `IPlaybackCoordinator.TriggerClipAsync`
 * the WPF UI itself calls (via the embedded REST API), so push-to-talk and polyphony behave
 * identically regardless of trigger source. The key's two States (idle / playing, see manifest.json)
 * are driven by the app's `/api/events` WebSocket push, not by Stream Deck's own automatic
 * click-to-toggle (`DisableAutomaticStates: true` in the manifest) — the key should reflect whether
 * the clip is *actually* playing, not just "was clicked an odd number of times".
 */
@action({ UUID: "com.soundboardlite.streamdeck.trigger-clip" })
export class TriggerClipAction extends SingletonAction<TriggerClipSettings> {
    private subscribed = false;

    /** Concurrent-play count per clip — a clip can be triggered again while already playing
     * (polyphony), so a single clipStopped must not flip the key back to idle while another
     * overlapping instance of the same clip is still going. */
    private activeCounts = new Map<string, number>();

    /** Clip name we last auto-filled as each key's title, and which keys have a real
     * user-typed title. The SDK has no "is this title user-set?" query — `titleParametersDidChange`
     * reports the live title text, so a title that's neither empty nor our own last fallback value
     * must be something the user typed, and we stop touching it from then on (until they clear it
     * back to empty, which we treat as "use the default again"). */
    private autoTitles = new Map<string, string>();
    private userOwnsTitle = new Set<string>();

    override onWillAppear(ev: WillAppearEvent<TriggerClipSettings>): void | Promise<void> {
        if (!this.subscribed) {
            this.subscribed = true;
            onPlaybackEvent((evt) => this.handlePlaybackEvent(evt));
        }
    }

    override onTitleParametersDidChange(ev: TitleParametersDidChangeEvent<TriggerClipSettings>): void | Promise<void> {
        const { action, payload } = ev;
        if (!action.isKey()) {
            return;
        }

        const { title } = payload;
        const { clipName } = payload.settings;

        if (title === "" || title === this.autoTitles.get(action.id)) {
            this.userOwnsTitle.delete(action.id);
        } else {
            this.userOwnsTitle.add(action.id);
            return;
        }

        if (clipName && title !== clipName) {
            this.autoTitles.set(action.id, clipName);
            return action.setTitle(clipName);
        }
    }

    override onDidReceiveSettings(ev: DidReceiveSettingsEvent<TriggerClipSettings>): void | Promise<void> {
        const { action, payload } = ev;
        if (!action.isKey() || this.userOwnsTitle.has(action.id)) {
            return;
        }

        const { clipName } = payload.settings;
        if (clipName) {
            this.autoTitles.set(action.id, clipName);
            return action.setTitle(clipName);
        }
    }

    override async onKeyDown(ev: KeyDownEvent<TriggerClipSettings>): Promise<void> {
        const { clipId } = ev.payload.settings;
        if (!clipId) {
            streamDeck.logger.warn("Trigger Clip key pressed with no clip configured in the property inspector.");
            await ev.action.showAlert();
            return;
        }

        try {
            await triggerClip(clipId);
        } catch (err) {
            streamDeck.logger.error(`Failed to trigger clip ${clipId}: ${err}`);
            await ev.action.showAlert();
        }
    }

    /** Flashes every visible key currently configured for the clip that just started/stopped. */
    private async handlePlaybackEvent(evt: PlaybackEvent): Promise<void> {
        if (evt.type !== "clipTriggered" && evt.type !== "clipStopped") {
            return;
        }
        if (!evt.clipId) {
            return;
        }

        const current = this.activeCounts.get(evt.clipId) ?? 0;
        const next = evt.type === "clipTriggered" ? current + 1 : Math.max(0, current - 1);
        this.activeCounts.set(evt.clipId, next);

        for (const instance of this.actions) {
            if (!instance.isKey()) {
                continue;
            }
            const settings = await instance.getSettings();
            if (settings.clipId !== evt.clipId) {
                continue;
            }
            await instance.setState(next > 0 ? 1 : 0);
        }
    }
}
