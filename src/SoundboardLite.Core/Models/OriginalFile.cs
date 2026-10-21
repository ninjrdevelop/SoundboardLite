namespace SoundboardLite.Core.Models;

/// <summary>
/// A ref-counted, de-duplicated, untouched import. Deleted from disk only when <see cref="RefCount"/> hits zero.
/// </summary>
public sealed class OriginalFile
{
    public required string Hash { get; init; }
    public required string FilePath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset ImportedUtc { get; init; }
    public required int RefCount { get; set; }
}
