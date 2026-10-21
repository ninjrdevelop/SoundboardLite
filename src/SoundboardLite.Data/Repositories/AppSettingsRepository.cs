using Dapper;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Data.Repositories;

public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AppSettingsRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Value FROM AppSettings WHERE Key = @key;", new { key });
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            """
            INSERT INTO AppSettings (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = @value;
            """,
            new { key, value });
    }
}
