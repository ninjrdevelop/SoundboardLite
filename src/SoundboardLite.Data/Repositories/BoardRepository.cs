using Dapper;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Data.Repositories;

public sealed class BoardRepository : IBoardRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public BoardRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Board>> ListAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        var boards = await connection.QueryAsync<Board>(
            "SELECT Id, Name, SortOrder, CreatedUtc, UpdatedUtc FROM Boards ORDER BY SortOrder;");
        return boards.ToList();
    }

    public async Task<Board?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<Board>(
            "SELECT Id, Name, SortOrder, CreatedUtc, UpdatedUtc FROM Boards WHERE Id = @id;",
            new { id = id.ToString() });
    }

    public async Task<Board> CreateAsync(string name, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        var nextSortOrder = await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(SortOrder) + 1, 0) FROM Boards;");

        var now = DateTimeOffset.UtcNow;
        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = name,
            SortOrder = nextSortOrder,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await connection.ExecuteAsync(
            """
            INSERT INTO Boards (Id, Name, SortOrder, CreatedUtc, UpdatedUtc)
            VALUES (@Id, @Name, @SortOrder, @CreatedUtc, @UpdatedUtc);
            """,
            new { Id = board.Id.ToString(), board.Name, board.SortOrder, board.CreatedUtc, board.UpdatedUtc });

        return board;
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Boards SET Name = @name, UpdatedUtc = @now WHERE Id = @id;",
            new { id = id.ToString(), name, now = DateTimeOffset.UtcNow });
    }

    public async Task ReorderAsync(Guid id, int sortOrder, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Boards SET SortOrder = @sortOrder, UpdatedUtc = @now WHERE Id = @id;",
            new { id = id.ToString(), sortOrder, now = DateTimeOffset.UtcNow });
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync("DELETE FROM Boards WHERE Id = @id;", new { id = id.ToString() });
    }
}
