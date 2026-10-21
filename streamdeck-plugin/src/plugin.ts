import streamDeck from "@elgato/streamdeck";
import { TriggerClipAction } from "./actions/trigger-clip.js";
import { StopClipAction } from "./actions/stop-clip.js";
import { StopAllAction } from "./actions/stop-all.js";
import { listBoards, listClips } from "./app-connection.js";

streamDeck.logger.setLevel("info");

streamDeck.actions.registerAction(new TriggerClipAction());
streamDeck.actions.registerAction(new StopClipAction());
streamDeck.actions.registerAction(new StopAllAction());

/**
 * The property inspector (ui/*.html) can't read api-session.json or call Soundboard Lite's API
 * directly (it's a sandboxed webview) — it asks the plugin for board/clip lists via sendToPlugin,
 * and the plugin answers via sendToPropertyInspector. Saving the chosen board/clip back onto the
 * key's own settings happens PI-side, directly over its own Stream Deck socket connection (the
 * standard `setSettings` protocol message) — the plugin isn't involved in that part.
 */
streamDeck.ui.onSendToPlugin(async (ev) => {
    const payload = ev.payload as { event?: string; boardId?: string };

    if (payload?.event === "requestBoards") {
        try {
            const boards = await listBoards();
            await streamDeck.ui.sendToPropertyInspector({ event: "boards", boards });
        } catch (err) {
            streamDeck.logger.error(`requestBoards failed: ${err}`);
            await streamDeck.ui.sendToPropertyInspector({ event: "error", message: String(err) });
        }
        return;
    }

    if (payload?.event === "requestClips" && payload.boardId) {
        try {
            const clips = await listClips(payload.boardId);
            await streamDeck.ui.sendToPropertyInspector({ event: "clips", boardId: payload.boardId, clips });
        } catch (err) {
            streamDeck.logger.error(`requestClips failed: ${err}`);
            await streamDeck.ui.sendToPropertyInspector({ event: "error", message: String(err) });
        }
    }
});

await streamDeck.connect();
