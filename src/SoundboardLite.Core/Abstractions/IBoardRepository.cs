using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

public interface IBoardRepository
{
    Task<IReadOnlyList<Board>> ListAsync(CancellationToken ct = default);
    Task<Board?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Board> CreateAsync(string name, CancellationToken ct = default);
    Task RenameAsync(Guid id, string name, CancellationToken ct = default);
    Task ReorderAsync(Guid id, int sortOrder, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
