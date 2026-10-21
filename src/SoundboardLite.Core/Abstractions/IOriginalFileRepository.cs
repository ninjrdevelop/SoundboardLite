using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

public interface IOriginalFileRepository
{
    Task<OriginalFile?> GetAsync(string hash, CancellationToken ct = default);

    /// <summary>Inserts a new row (RefCount=1) or increments RefCount if the hash already exists.</summary>
    Task AddReferenceAsync(string hash, string filePath, long sizeBytes, CancellationToken ct = default);

    /// <summary>Decrements RefCount; returns true (and the caller should delete the file) if it reached zero.</summary>
    Task<bool> ReleaseReferenceAsync(string hash, CancellationToken ct = default);
}
