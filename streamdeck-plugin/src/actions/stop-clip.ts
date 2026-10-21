import streamDeck, { action, KeyDownEvent, SingletonAction } from "@elgato/streamdeck";
import type { JsonObject } from "@elgato/utils";
import { stopClip } from "../app-connection.js";

export type StopClipSettings = JsonObject & {
    boardId?: string;
    boardName?: string;
    clipId?: string;
    clipName?: string;
};

/** One key = stop one specific clip (all of its concurrently-playing instances). */
@action({ UUID: "com.soundboardlite.streamdeck.stop-clip" })
export class StopClipAction extends SingletonAction<StopClipSettings> {
    override async onKeyDown(ev: KeyDownEvent<StopClipSettings>): Promise<void> {
        const { clipId } = ev.payload.settings;
        if (!clipId) {
            streamDeck.logger.warn("Stop Clip key pressed with no clip configured in the property inspector.");
            await ev.action.showAlert();
            return;
        }

        try {
            await stopClip(clipId);
        } catch (err) {
            streamDeck.logger.error(`Failed to stop clip ${clipId}: ${err}`);
            await ev.action.showAlert();
        }
    }
}
