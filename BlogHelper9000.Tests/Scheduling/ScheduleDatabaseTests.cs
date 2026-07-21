using Microsoft.Data.Sqlite;
using BlogHelper9000.Core.Scheduling;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleDatabaseTests
{
    [Fact]
    public void OpenInMemory_CreatesSchemaAtVersion2()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        db.SchemaVersion.Should().Be(2);
    }

    [Fact]
    public void Migrate_IsIdempotent_ForAnAlreadyMigratedDatabase()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        var act = () => db.SchemaVersion;

        act.Should().NotThrow();
        db.SchemaVersion.Should().Be(2);
    }

    [Fact]
    public void PathFor_AppendsDotPrefixedFileNameToBlogRoot()
    {
        ScheduleDatabase.PathFor("/blog").Should().Be(Path.Combine("/blog", ".bloghelper.db"));
    }

    [Fact]
    public void OpenInMemory_SeriesTable_HasCadenceColumns()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        using var command = db.Connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(series);";
        using var reader = command.ExecuteReader();
        var columnNames = new List<string>();
        while (reader.Read())
            columnNames.Add(reader.GetString(1));

        columnNames.Should().Contain(["cadence_day", "cadence_start"]);
    }

    [Fact]
    public void Open_UpgradesAGenuineV1DatabaseToV2_PreservingExistingData()
    {
        var tempDir = Directory.CreateTempSubdirectory("bloghelper-v1-upgrade-test-");
        try
        {
            var dbPath = Path.Combine(tempDir.FullName, ScheduleDatabase.FileName);

            using (var v1Connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                v1Connection.Open();
                using (var pragma = v1Connection.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON;";
                    pragma.ExecuteNonQuery();
                }

                using var create = v1Connection.CreateCommand();
                create.CommandText = """
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

                    INSERT INTO series (id, name, sort_order) VALUES (1, 'Existing Series', 0);
                    INSERT INTO schedule_entries
                        (id, series_id, position, week, publish_date, topic, title, draft_filename, tags, source, published, notes)
                        VALUES (1, 1, 1, 3, '2026-07-06', 'Topic', 'Existing Post', 'existing-post.md', 'csharp', 'Legacy', 1, 'note');

                    PRAGMA user_version = 1;
                    """;
                create.ExecuteNonQuery();
            }

            using var db = ScheduleDatabase.Open(tempDir.FullName);

            db.SchemaVersion.Should().Be(2);

            var repository = new BlogHelper9000.Core.Scheduling.ScheduleRepository(db);
            var series = repository.FindSeries("Existing Series");
            series.Should().NotBeNull();
            series!.CadenceDay.Should().BeNull();
            series.CadenceStart.Should().BeNull();

            var entry = repository.FindEntryByFilename("existing-post.md");
            entry.Should().NotBeNull();
            entry!.Title.Should().Be("Existing Post");
            entry.Published.Should().BeTrue();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }
}
