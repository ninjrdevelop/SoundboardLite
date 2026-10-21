namespace SoundboardLite.Core.Models;

/// <summary>
/// The subset of a clip's state that determines its rendered audio. Deliberately split from
/// <see cref="Clip"/> so "does this change require a re-render" stays isolated from identity/display fields.
/// </summary>
public sealed class ClipEditSettings
{
    public required Guid ClipId { get; init; }

    public required long TrimStartMs { get; set; }

    /// <summary>Null means "end of the original file".</summary>
    public long? TrimEndMs { get; set; }

    public required double PitchSemitones { get; set; }
    public required double GainDb { get; set; }
    public required long FadeInMs { get; set; }
    public required long FadeOutMs { get; set; }

    /// <summary>Hash of (OriginalFileHash + these settings); null until the first background render completes.</summary>
    public string? CurrentRenderKey { get; set; }

    public required bool PttEnabled { get; set; }

    /// <summary>Win32 virtual-key code. Null falls back to the global default in AppSettings.</summary>
    public int? PttKeyOverride { get; set; }
    public int? PttBeginDelayMsOverride { get; set; }
    public int? PttEndDelayMsOverride { get; set; }

    public required DateTimeOffset UpdatedUtc { get; set; }

    public static ClipEditSettings CreateDefault(Guid clipId, DateTimeOffset now) => new()
    {
        ClipId = clipId,
        TrimStartMs = 0,
        TrimEndMs = null,
        PitchSemitones = 0,
        GainDb = 0,
        FadeInMs = 0,
        FadeOutMs = 0,
        CurrentRenderKey = null,
        PttEnabled = false,
        PttKeyOverride = null,
        PttBeginDelayMsOverride = null,
        PttEndDelayMsOverride = null,
        UpdatedUtc = now,
    };
}
