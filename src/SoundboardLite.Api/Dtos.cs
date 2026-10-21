using System.Text.Json.Serialization;

namespace SoundboardLite.Api;

public sealed record BoardDto(Guid Id, string Name, int SortOrder, int ClipCount);

public sealed record ClipDto(Guid Id, string Name, string? ColorTag, long DurationMs, bool IsReady);

public sealed record TriggerResponseDto(Guid PlaybackInstanceId);

public sealed record OutputDeviceDto(string Id, string Name, bool IsSelected);

public sealed record HealthDto(string Status);

/// <summary>Pushed over <c>/api/events</c>. <c>Type</c> is one of: clipTriggered, clipStopped,
/// boardChanged, clipRenderReady.</summary>
public sealed record WsEventDto(string Type, Guid? ClipId, Guid? BoardId);

public sealed record ApiSession(int Port, string Token, int Pid, DateTimeOffset StartedUtc);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BoardDto))]
[JsonSerializable(typeof(BoardDto[]))]
[JsonSerializable(typeof(ClipDto))]
[JsonSerializable(typeof(ClipDto[]))]
[JsonSerializable(typeof(TriggerResponseDto))]
[JsonSerializable(typeof(OutputDeviceDto))]
[JsonSerializable(typeof(OutputDeviceDto[]))]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(WsEventDto))]
[JsonSerializable(typeof(ApiSession))]
internal partial class ApiJsonContext : JsonSerializerContext;
