using Microsoft.Data.Sqlite;

namespace BlogHelper9000.Core.Scheduling;

/// <summary>
/// Owns the SQLite connection for the publishing schedule. The file is dot-prefixed
/// because Jekyll copies non-underscore root files into the generated site; dotfiles
/// are excluded by default.
/// </summary>
public sealed class ScheduleDatabase : IDisposable
{
    public const string FileName = ".bloghelper.db";

    private readonly SqliteConnection _connection;

    private ScheduleDatabase(SqliteConnection connection)
    {
        _connection = connection;
        _connection.Open();
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
        Migrate();
    }

    public static ScheduleDatabase Open(string blogBaseDirectory) =>
        new(new SqliteConnection($"Data Source={PathFor(blogBaseDirectory)}"));

    public static ScheduleDatabase OpenInMemory() =>
        new(new SqliteConnection("Data Source=:memory:"));

    public static string PathFor(string blogBaseDirectory) =>
        Path.Combine(blogBaseDirectory, FileName);

    internal SqliteConnection Connection => _connection;

    public long SchemaVersion
    {
        get
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return (long)command.ExecuteScalar()!;
        }
    }

    private void Migrate()
    {
        if (SchemaVersion >= 1) return;

        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE meta (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL);

            CREATE TABLE series (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                name       TEXT NOT NULL UNIQUE COLLATE NOCASE,
                sort_order INTEGER NOT NULL DEFAULT 0);

            CREATE TABLE schedule_entries (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                series_id      INTEGER NOT NULL REFERENCES series(id) ON DELETE CASCADE,
                position       INTEGER NOT NULL,
                week           INTEGER NULL,
                publish_date   TEXT NULL,
                topic          TEXT NULL,
                title          TEXT NOT NULL,
                draft_filename TEXT NOT NULL,
                tags           TEXT NULL,
                source         TEXT NULL,
                published      INTEGER NOT NULL DEFAULT 0,
                published_on   TEXT NULL,
                notes          TEXT NULL,
                UNIQUE(series_id, position));

            CREATE INDEX ix_schedule_entries_filename ON schedule_entries(draft_filename);

            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void Dispose() => _connection.Dispose();
}
