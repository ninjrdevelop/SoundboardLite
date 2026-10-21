using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

/// <summary>
/// Hashes + copies a source audio file into the untouched originals store (de-duplicating by content
/// hash), probes its duration, and persists the resulting Clip + default ClipEditSettings.
/// </summary>
public interface IClipImportService
{
    Task<Clip> ImportAsync(string sourceFilePath, Guid boardId, CancellationToken ct = default);
}
