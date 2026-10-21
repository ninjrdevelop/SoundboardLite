import streamDeck, { action, KeyDownEvent, SingletonAction } from "@elgato/streamdeck";
import { stopAll } from "../app-connection.js";

/** No settings — one "panic button" key that stops every currently-playing clip. */
@action({ UUID: "com.soundboardlite.streamdeck.stop-all" })
export class StopAllAction extends SingletonAction {
    override async onKeyDown(ev: KeyDownEvent): Promise<void> {
        try {
            await stopAll();
        } catch (err) {
            streamDeck.logger.error(`Failed to stop all playback: ${err}`);
            await ev.action.showAlert();
        }
    }
}
