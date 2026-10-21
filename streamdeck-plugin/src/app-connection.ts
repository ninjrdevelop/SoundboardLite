/**
 * Talks to Soundboard Lite's embedded local API (SoundboardLite.Api): discovers the current
 * port/token by reading `%LOCALAPPDATA%\SoundboardLite\runtime\api-session.json` (written by the
 * app on startup, deleted on graceful shutdown), and re-reads it whenever a request fails — so a
 * restart of the app (new port, new token) is picked up transparently without restarting the plugin.
 */
import { readFile } from "node:fs/promises";
import { WebSocket } from "ws";
import streamDeck from "@elgato/streamdeck";
import type { JsonObject } from "@elgato/utils";

interface ApiSession {
    port: number;
    token: string;
    pid: number;
    startedUtc: string;
}

export type BoardDto = JsonObject & {
    id: string;
    name: string;
    sortOrder: number;
    clipCount: number;
};

export type ClipDto = JsonObject & {
    id: string;
    name: string;
    colorTag: string | null;
    durationMs: number;
    isReady: boolean;
};

export type PlaybackEvent = JsonObject & {
    type: "clipTriggered" | "clipStopped" | "boardChanged" | "clipRenderReady";
    clipId: string | null;
    boardId: string | null;
};

const SESSION_FILE_PATH = `${process.env.LOCALAPPDATA}\\SoundboardLite\\runtime\\api-session.json`;

let cachedSession: ApiSession | null = null;

async function readSession(): Promise<ApiSession | null> {
    try {
        const raw = await readFile(SESSION_FILE_PATH, "utf-8");
        return JSON.parse(raw) as ApiSession;
    } catch {
        return null;
    }
}

async function getSession(): Promise<ApiSession> {
    if (cachedSession) {
        return cachedSession;
    }
    const session = await readSession();
    if (!session) {
        throw new Error("Soundboard Lite is not running (no api-session.json found).");
    }
    cachedSession = session;
    return session;
}

/**
 * Issues a request against the embedded API. On a 401 (stale token) or a connection failure (app
 * restarted / port changed), drops the cached session and retries exactly once against a freshly
 * re-read session file — this is the "re-read on failure" behaviour the app's handover notes
 * describe as how the plugin should handle the app restarting.
 */
async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
    let lastError: unknown;
    for (let attempt = 0; attempt < 2; attempt++) {
        let session: ApiSession;
        try {
            session = await getSession();
        } catch (err) {
            lastError = err;
            break;
        }

        try {
            const response = await fetch(`http://127.0.0.1:${session.port}${path}`, {
                ...init,
                headers: { ...(init.headers ?? {}), Authorization: `Bearer ${session.token}` },
            });
            if (response.status === 401 && attempt === 0) {
                cachedSession = null;
                continue;
            }
            return response;
        } catch (err) {
            lastError = err;
            cachedSession = null;
        }
    }
    throw lastError instanceof Error ? lastError : new Error("Soundboard Lite API request failed.");
}

export async function listBoards(): Promise<BoardDto[]> {
    const response = await apiFetch("/api/boards");
    if (!response.ok) {
        throw new Error(`GET /api/boards failed: ${response.status}`);
    }
    return (await response.json()) as BoardDto[];
}

export async function listClips(boardId: string): Promise<ClipDto[]> {
    const response = await apiFetch(`/api/boards/${boardId}/clips`);
    if (!response.ok) {
        throw new Error(`GET /api/boards/${boardId}/clips failed: ${response.status}`);
    }
    return (await response.json()) as ClipDto[];
}

export async function triggerClip(clipId: string): Promise<void> {
    const response = await apiFetch(`/api/clips/${clipId}/trigger`, { method: "POST" });
    if (!response.ok) {
        throw new Error(`POST /api/clips/${clipId}/trigger failed: ${response.status}`);
    }
}

export async function stopClip(clipId: string): Promise<void> {
    const response = await apiFetch(`/api/clips/${clipId}/stop`, { method: "POST" });
    if (!response.ok) {
        throw new Error(`POST /api/clips/${clipId}/stop failed: ${response.status}`);
    }
}

export async function stopAll(): Promise<void> {
    const response = await apiFetch("/api/playback/stop-all", { method: "POST" });
    if (!response.ok) {
        throw new Error(`POST /api/playback/stop-all failed: ${response.status}`);
    }
}

// --- /api/events WebSocket: pushes key-state feedback (flash while playing) to all listeners. ---

const listeners = new Set<(evt: PlaybackEvent) => void>();
let eventSocket: WebSocket | null = null;
let reconnectTimer: NodeJS.Timeout | null = null;

export function onPlaybackEvent(listener: (evt: PlaybackEvent) => void): () => void {
    listeners.add(listener);
    ensureEventSocket();
    return () => listeners.delete(listener);
}

function ensureEventSocket(): void {
    if (eventSocket && eventSocket.readyState <= WebSocket.OPEN) {
        return;
    }

    getSession()
        .then((session) => {
            const socket = new WebSocket(`ws://127.0.0.1:${session.port}/api/events`, {
                headers: { Authorization: `Bearer ${session.token}` },
            });

            socket.on("message", (data: Buffer) => {
                try {
                    const evt = JSON.parse(data.toString()) as PlaybackEvent;
                    for (const listener of listeners) {
                        listener(evt);
                    }
                } catch (err) {
                    streamDeck.logger.warn(`Ignoring malformed /api/events frame: ${err}`);
                }
            });

            socket.on("close", () => {
                eventSocket = null;
                cachedSession = null;
                scheduleReconnect();
            });

            socket.on("error", (err) => {
                streamDeck.logger.warn(`/api/events socket error: ${err}`);
            });

            eventSocket = socket;
        })
        .catch(() => {
            scheduleReconnect();
        });
}

function scheduleReconnect(): void {
    if (reconnectTimer || listeners.size === 0) {
        return;
    }
    reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        if (listeners.size > 0) {
            ensureEventSocket();
        }
    }, 3000);
}
