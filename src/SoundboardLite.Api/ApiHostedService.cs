using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Api;

/// <summary>
/// Hosts the embedded local HTTP/WebSocket API as an <see cref="IHostedService"/> inside the same
/// generic host the WPF app already runs (started/stopped automatically alongside it via
/// <c>IHost.StartAsync</c>/<c>StopAsync</c>). Builds its own independent <see cref="WebApplication"/>
/// (Kestrel, loopback-only, OS-assigned port) rather than sharing the outer host's DI container, but
/// deliberately registers the *same already-constructed* repository/coordinator instances into it
/// (not new ones) — in particular <see cref="IPlaybackCoordinator"/> must be the one real singleton
/// the WPF UI also triggers through, or the API and the UI would silently control two independent
/// mixers/output devices.
/// </summary>
public sealed class ApiHostedService : IHostedService
{
    private readonly IBoardRepository _boardRepository;
    private readonly IClipRepository _clipRepository;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IPlaybackCoordinator _playbackCoordinator;
    private readonly IRenderQueue _renderQueue;
    private readonly IAppPaths _appPaths;
    private readonly EventHub _eventHub;

    private WebApplication? _app;

    public ApiHostedService(
        IBoardRepository boardRepository,
        IClipRepository clipRepository,
        IOriginalFileRepository originalFileRepository,
        IPlaybackCoordinator playbackCoordinator,
        IRenderQueue renderQueue,
        IAppPaths appPaths,
        EventHub eventHub)
    {
        _boardRepository = boardRepository;
        _clipRepository = clipRepository;
        _originalFileRepository = originalFileRepository;
        _playbackCoordinator = playbackCoordinator;
        _renderQueue = renderQueue;
        _appPaths = appPaths;
        _eventHub = eventHub;

        _playbackCoordinator.ClipStarted += OnClipStarted;
        _playbackCoordinator.ClipStopped += OnClipStopped;
        _renderQueue.RenderCompleted += OnRenderCompleted;
    }

    private void OnClipStarted(object? sender, Guid clipId) =>
        _ = _eventHub.BroadcastAsync(new WsEventDto("clipTriggered", clipId, null));

    private void OnClipStopped(object? sender, Guid clipId) =>
        _ = _eventHub.BroadcastAsync(new WsEventDto("clipStopped", clipId, null));

    private void OnRenderCompleted(object? sender, RenderCompletedEventArgs args)
    {
        if (args.Success)
        {
            _ = _eventHub.BroadcastAsync(new WsEventDto("clipRenderReady", args.ClipId, null));
        }
    }

    /// <summary>Called by the App layer after board/clip CRUD so the plugin can refresh its cached
    /// board/clip lists without polling.</summary>
    public Task BroadcastBoardChangedAsync(Guid? boardId) =>
        _eventHub.BroadcastAsync(new WsEventDto("boardChanged", null, boardId));

    public async Task StartAsync(CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // port 0 = OS-assigned ephemeral port

        // Reuse the exact same singleton instances the WPF app already uses — see class remarks.
        builder.Services.AddSingleton(_boardRepository);
        builder.Services.AddSingleton(_clipRepository);
        builder.Services.AddSingleton(_originalFileRepository);
        builder.Services.AddSingleton(_playbackCoordinator);
        builder.Services.AddSingleton(_eventHub);

        _app = builder.Build();
        _app.UseWebSockets();
        _app.Use(next => TokenAuth.Middleware(next, token));
        MapEndpoints(_app);

        await _app.StartAsync(ct);

        var actualPort = new Uri(_app.Urls.First()).Port;
        ApiSessionFile.Write(_appPaths, new ApiSession(actualPort, token, Environment.ProcessId, DateTimeOffset.UtcNow));
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _playbackCoordinator.ClipStarted -= OnClipStarted;
        _playbackCoordinator.ClipStopped -= OnClipStopped;
        _renderQueue.RenderCompleted -= OnRenderCompleted;

        ApiSessionFile.Delete(_appPaths);

        if (_app is not null)
        {
            await _app.StopAsync(ct);
            await _app.DisposeAsync();
        }
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/health", () =>
            Results.Json(new HealthDto("ok"), ApiJsonContext.Default.HealthDto));

        app.MapGet("/api/boards", async (IBoardRepository boards, IClipRepository clips, CancellationToken ct) =>
        {
            var boardList = await boards.ListAsync(ct);
            var dtos = new List<BoardDto>(boardList.Count);
            foreach (var board in boardList)
            {
                var clipList = await clips.ListByBoardAsync(board.Id, ct);
                dtos.Add(new BoardDto(board.Id, board.Name, board.SortOrder, clipList.Count));
            }
            return Results.Json(dtos.ToArray(), ApiJsonContext.Default.BoardDtoArray);
        });

        app.MapGet("/api/boards/{boardId:guid}/clips", async (
            Guid boardId, IClipRepository clips, IOriginalFileRepository originals, CancellationToken ct) =>
        {
            var clipList = await clips.ListByBoardAsync(boardId, ct);
            var dtos = new List<ClipDto>(clipList.Count);
            foreach (var clip in clipList)
            {
                var original = await originals.GetAsync(clip.OriginalFileHash, ct);
                var isReady = original is not null && File.Exists(original.FilePath);
                dtos.Add(new ClipDto(clip.Id, clip.Name, clip.ColorTag, clip.OriginalDurationMs, isReady));
            }
            return Results.Json(dtos.ToArray(), ApiJsonContext.Default.ClipDtoArray);
        });

        app.MapPost("/api/clips/{clipId:guid}/trigger", async (
            Guid clipId, IPlaybackCoordinator playback, CancellationToken ct) =>
        {
            try
            {
                await playback.TriggerClipAsync(clipId, ct);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }

            // A fresh correlation id for this trigger request — IPlaybackCoordinator doesn't track
            // per-instance ids internally (stop is clip-id-based, matching the existing StopClip
            // semantics used by the WPF UI), so this doesn't identify a stoppable handle.
            return Results.Json(
                new TriggerResponseDto(Guid.NewGuid()),
                ApiJsonContext.Default.TriggerResponseDto,
                statusCode: StatusCodes.Status202Accepted);
        });

        app.MapPost("/api/clips/{clipId:guid}/stop", (Guid clipId, IPlaybackCoordinator playback) =>
        {
            playback.StopClip(clipId);
            return Results.NoContent();
        });

        app.MapPost("/api/playback/stop-all", (IPlaybackCoordinator playback) =>
        {
            playback.StopAll();
            return Results.NoContent();
        });

        app.MapGet("/api/output-devices", (IPlaybackCoordinator playback) =>
        {
            var dtos = playback.GetOutputDevices()
                .Select(d => new OutputDeviceDto(d.Id, d.Name, d.Id == playback.CurrentOutputDeviceId))
                .ToArray();
            return Results.Json(dtos, ApiJsonContext.Default.OutputDeviceDtoArray);
        });

        app.Map("/api/events", async (HttpContext context, EventHub hub) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await hub.HandleConnectionAsync(socket, context.RequestAborted);
        });
    }
}
