using Dapper;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Data.Repositories;

public sealed class OriginalFileRepository : IOriginalFileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public OriginalFileRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<OriginalFile?> GetAsync(string hash, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<OriginalFile>(
            "SELECT Hash, FilePath, SizeBytes, ImportedUtc, RefCount FROM OriginalFiles WHERE Hash = @hash;",
            new { hash });
    }

    public async Task AddReferenceAsync(string hash, string filePath, long sizeBytes, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            """
            INSERT INTO OriginalFiles (Hash, FilePath, SizeBytes, ImportedUtc, RefCount)
            VALUES (@hash, @filePath, @sizeBytes, @now, 1)
            ON CONFLICT(Hash) DO UPDATE SET RefCount = RefCount + 1;
            """,
            new { hash, filePath, sizeBytes, now = DateTimeOffset.UtcNow });
    }

    public async Task<bool> ReleaseReferenceAsync(string hash, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE OriginalFiles SET RefCount = RefCount - 1 WHERE Hash = @hash;", new { hash });

        var remaining = await connection.ExecuteScalarAsync<int?>(
            "SELECT RefCount FROM OriginalFiles WHERE Hash = @hash;", new { hash });

        if (remaining is null or > 0) return false;

        await connection.ExecuteAsync("DELETE FROM OriginalFiles WHERE Hash = @hash;", new { hash });
        return true;
    }
}
