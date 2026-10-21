using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

public interface IClipRepository
{
    Task<IReadOnlyList<Clip>> ListByBoardAsync(Guid boardId, CancellationToken ct = default);
    Task<Clip?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ClipEditSettings?> GetEditSettingsAsync(Guid clipId, CancellationToken ct = default);

    /// <summary>All non-null CurrentRenderKey values across every clip, for render-cache GC.</summary>
    Task<IReadOnlyCollection<string>> GetAllRenderKeysAsync(CancellationToken ct = default);

    /// <summary>Inserts the clip and its default edit settings in one transaction.</summary>
    Task CreateAsync(Clip clip, ClipEditSettings editSettings, CancellationToken ct = default);

    Task UpdateAsync(Clip clip, CancellationToken ct = default);
    Task UpdateEditSettingsAsync(ClipEditSettings editSettings, CancellationToken ct = default);
    Task ReorderAsync(Guid id, int sortOrder, CancellationToken ct = default);

    /// <summary>Deletes the clip row and returns the OriginalFileHash it referenced, for ref-count decrement.</summary>
    Task<string> DeleteAsync(Guid id, CancellationToken ct = default);
}
