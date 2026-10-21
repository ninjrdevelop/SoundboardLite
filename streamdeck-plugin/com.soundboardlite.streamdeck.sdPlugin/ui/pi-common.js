// Shared property-inspector boilerplate: the PI runs in a sandboxed webview with no access to
// api-session.json or the filesystem, so it can't call Soundboard Lite's REST API directly. It asks
// the plugin process (which does have that access) for data via `sendToPlugin`, and the plugin
// answers via `sendToPropertyInspector` (see src/plugin.ts's onSendToPlugin handler). Persisting the
// chosen board/clip onto the key's own settings happens here directly, via the standard `setSettings`
// protocol message sent over this same socket — the plugin isn't involved in that part.
//
// `connectElgatoStreamDeckSocket` is called by the Stream Deck application itself once it loads this
// page; the function name and parameter order are fixed by the Stream Deck PI protocol.
let websocket = null;
let piUuid = null;
let actionInfo = null;

function connectElgatoStreamDeckSocket(inPort, inPropertyInspectorUUID, inRegisterEvent, inInfo, inActionInfo) {
    piUuid = inPropertyInspectorUUID;
    actionInfo = JSON.parse(inActionInfo);

    websocket = new WebSocket("ws://127.0.0.1:" + inPort);

    websocket.onopen = () => {
        websocket.send(JSON.stringify({ event: inRegisterEvent, uuid: piUuid }));
        if (typeof onConnected === "function") {
            onConnected((actionInfo.payload && actionInfo.payload.settings) || {});
        }
    };

    websocket.onmessage = (evt) => {
        const message = JSON.parse(evt.data);
        if (message.event === "sendToPropertyInspector" && typeof onPluginMessage === "function") {
            onPluginMessage(message.payload);
        }
    };
}

function sendToPlugin(payload) {
    if (!websocket || websocket.readyState !== WebSocket.OPEN) {
        return;
    }
    websocket.send(JSON.stringify({ action: actionInfo.action, event: "sendToPlugin", context: piUuid, payload }));
}

function saveSettings(settings) {
    if (!websocket || websocket.readyState !== WebSocket.OPEN) {
        return;
    }
    websocket.send(
        JSON.stringify({ action: actionInfo.action, event: "setSettings", context: piUuid, payload: settings }),
    );
}
