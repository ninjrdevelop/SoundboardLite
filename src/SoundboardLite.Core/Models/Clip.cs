namespace SoundboardLite.Core.Models;

public sealed class Clip
{
    public required Guid Id { get; init; }
    public required Guid BoardId { get; set; }
    public required string Name { get; set; }

    /// <summary>SHA-256 hash of the untouched original file, keyed into originals/{hash}{ext}.</summary>
    public required string OriginalFileHash { get; set; }
    public required string OriginalFileExtension { get; set; }
    public required long OriginalDurationMs { get; set; }

    public required int SortOrder { get; set; }
    public string? ColorTag { get; set; }

    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset UpdatedUtc { get; set; }
}
