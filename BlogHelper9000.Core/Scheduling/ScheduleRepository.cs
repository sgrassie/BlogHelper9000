using Microsoft.Data.Sqlite;

namespace BlogHelper9000.Core.Scheduling;

public sealed class ScheduleRepository(ScheduleDatabase database)
{
    private SqliteConnection Connection => database.Connection;

    public long AddSeries(string name, int sortOrder = 0)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            "INSERT INTO series (name, sort_order) VALUES ($name, $sort); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$sort", sortOrder);
        return (long)command.ExecuteScalar()!;
    }

    public IReadOnlyList<SeriesInfo> ListSeries()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "SELECT id, name, sort_order FROM series ORDER BY sort_order, id;";
        using var reader = command.ExecuteReader();
        var result = new List<SeriesInfo>();
        while (reader.Read())
            result.Add(new SeriesInfo(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2)));
        return result;
    }

    public SeriesInfo? FindSeries(string name)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            "SELECT id, name, sort_order FROM series WHERE name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new SeriesInfo(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2))
            : null;
    }

    public long AddEntry(long seriesId, NewScheduleEntry entry)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO schedule_entries
                (series_id, position, week, publish_date, topic, title,
                 draft_filename, tags, source, published, notes)
            VALUES
                ($series,
                 COALESCE($position,
                     (SELECT COALESCE(MAX(position), 0) + 1 FROM schedule_entries WHERE series_id = $series)),
                 $week, $date, $topic, $title, $filename, $tags, $source, $published, $notes);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$series", seriesId);
        command.Parameters.AddWithValue("$position", (object?)entry.Position ?? DBNull.Value);
        command.Parameters.AddWithValue("$week", (object?)entry.Week ?? DBNull.Value);
        command.Parameters.AddWithValue("$date",
            (object?)entry.PublishDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$topic", (object?)entry.Topic ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", entry.Title);
        command.Parameters.AddWithValue("$filename", entry.DraftFilename);
        command.Parameters.AddWithValue("$tags", (object?)entry.Tags ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", (object?)entry.Source ?? DBNull.Value);
        command.Parameters.AddWithValue("$published", entry.Published ? 1 : 0);
        command.Parameters.AddWithValue("$notes", (object?)entry.Notes ?? DBNull.Value);
        return (long)command.ExecuteScalar()!;
    }

    private const string EntrySelect = """
        SELECT e.id, s.name, e.position, e.week, e.publish_date, e.topic, e.title,
               e.draft_filename, e.tags, e.source, e.published, e.published_on, e.notes
        FROM schedule_entries e
        JOIN series s ON s.id = e.series_id
        """;

    public IReadOnlyList<ScheduleEntry> GetEntries(long seriesId)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = EntrySelect + " WHERE e.series_id = $series ORDER BY e.position;";
        command.Parameters.AddWithValue("$series", seriesId);
        using var reader = command.ExecuteReader();
        var result = new List<ScheduleEntry>();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    public ScheduleEntry? FindEntryByFilename(string draftFilename)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = EntrySelect + " WHERE e.draft_filename = $filename LIMIT 1;";
        command.Parameters.AddWithValue("$filename", draftFilename);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    public void SetPublished(long entryId, bool published, DateOnly? publishedOn)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            "UPDATE schedule_entries SET published = $published, published_on = $on WHERE id = $id;";
        command.Parameters.AddWithValue("$published", published ? 1 : 0);
        command.Parameters.AddWithValue("$on",
            (object?)publishedOn?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", entryId);
        command.ExecuteNonQuery();
    }

    public void SetMeta(string key, string value)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public string? GetMeta(string key)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static ScheduleEntry ReadEntry(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetInt32(2),
        reader.IsDBNull(3) ? null : reader.GetInt32(3),
        reader.IsDBNull(4) ? null : DateOnly.Parse(reader.GetString(4)),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.GetInt32(10) == 1,
        reader.IsDBNull(11) ? null : DateOnly.Parse(reader.GetString(11)),
        reader.IsDBNull(12) ? null : reader.GetString(12));
}
