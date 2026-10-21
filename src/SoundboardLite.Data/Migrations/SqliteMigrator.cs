using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SoundboardLite.Data.Migrations;

/// <summary>
/// Hand-rolled migration runner: embedded "schema_NNN_description.sql" scripts applied in order,
/// tracked via SQLite's built-in PRAGMA user_version. Deliberately not a full migration framework —
/// the schema is five tables; this is ~30 lines instead of a new dependency.
/// </summary>
internal static partial class SqliteMigrator
{
    public static void Apply(SqliteConnection connection)
    {
        var currentVersion = GetUserVersion(connection);

        var assembly = typeof(SqliteMigrator).Assembly;
        var pending = assembly.GetManifestResourceNames()
            .Select(name => (Name: name, Match: ScriptNameRegex().Match(name)))
            .Where(x => x.Match.Success)
            .Select(x => (x.Name, Version: int.Parse(x.Match.Groups["version"].Value)))
            .Where(x => x.Version > currentVersion)
            .OrderBy(x => x.Version)
            .ToList();

        foreach (var (resourceName, version) in pending)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' not found.");
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
            transaction.Commit();

            SetUserVersion(connection, version);
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }

    [GeneratedRegex(@"schema_(?<version>\d+)_.*\.sql$")]
    private static partial Regex ScriptNameRegex();
}
