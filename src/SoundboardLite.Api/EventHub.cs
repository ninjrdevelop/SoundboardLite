using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace SoundboardLite.Api;

/// <summary>
/// Tracks connected <c>/api/events</c> WebSocket clients and broadcasts <see cref="WsEventDto"/>
/// frames to all of them — lets the Stream Deck plugin flash a key while a clip plays and refresh
/// its cached board/clip lists, without polling.
/// </summary>
public sealed class EventHub
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        try
        {
            var buffer = new byte[16];
            while (socket.State == WebSocketState.Open)
            {
                // The plugin never needs to send anything; we just wait here until it closes so the
                // connection's lifetime (and this handler's) is tied to the socket actually being open.
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutting down.
        }
        catch (WebSocketException)
        {
            // Client disconnected abruptly.
        }
        finally
        {
            _sockets.TryRemove(id, out _);
        }
    }

    public async Task BroadcastAsync(WsEventDto evt)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(evt, ApiJsonContext.Default.WsEventDto);

        foreach (var (id, socket) in _sockets)
        {
            if (socket.State != WebSocketState.Open)
            {
                _sockets.TryRemove(id, out _);
                continue;
            }

            try
            {
                await socket.SendAsync(json, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                _sockets.TryRemove(id, out _);
            }
        }
    }
}
