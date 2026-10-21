using Microsoft.Data.Sqlite;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Data.Migrations;

namespace SoundboardLite.Data;

/// <summary>
/// Opens connections to the library database, applying the pragmas every connection needs
/// (foreign keys, WAL for concurrent read/write) and running pending migrations on first use.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly object _migrationLock = new();
    private bool _migrated;

    public SqliteConnectionFactory(IAppPaths appPaths)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = appPaths.DatabaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            pragma.ExecuteNonQuery();
        }

        EnsureMigrated(connection);
        return connection;
    }

    private void EnsureMigrated(SqliteConnection connection)
    {
        if (_migrated) return;
        lock (_migrationLock)
        {
            if (_migrated) return;
            SqliteMigrator.Apply(connection);
            _migrated = true;
        }
    }
}
