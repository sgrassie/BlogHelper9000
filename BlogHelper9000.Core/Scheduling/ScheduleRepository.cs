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

    private const string SeriesSelect =
        "SELECT id, name, sort_order, cadence_day, cadence_start FROM series";

    public IReadOnlyList<SeriesInfo> ListSeries()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = SeriesSelect + " ORDER BY sort_order, id;";
        using var reader = command.ExecuteReader();
        var result = new List<SeriesInfo>();
        while (reader.Read())
            result.Add(ReadSeries(reader));
        return result;
    }

    public SeriesInfo? FindSeries(string name)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = SeriesSelect + " WHERE name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSeries(reader) : null;
    }

    private static SeriesInfo ReadSeries(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetInt32(2),
        reader.IsDBNull(3) ? null : (DayOfWeek)reader.GetInt32(3),
        reader.IsDBNull(4) ? null : DateOnly.ParseExact(reader.GetString(4), "yyyy-MM-dd"));

    public void RenameSeries(long seriesId, string newName)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "UPDATE series SET name = $name WHERE id = $id;";
        command.Parameters.AddWithValue("$name", newName);
        command.Parameters.AddWithValue("$id", seriesId);
        command.ExecuteNonQuery();
    }

    /// <summary>Deletes the series. Relies on FK CASCADE to remove its schedule entries;
    /// callers are responsible for enforcing any "series must be empty" policy.</summary>
    public void DeleteSeries(long seriesId)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "DELETE FROM series WHERE id = $id;";
        command.Parameters.AddWithValue("$id", seriesId);
        command.ExecuteNonQuery();
    }

    public void SetSeriesCadence(long seriesId, int? cadenceDay, string? cadenceStart)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            "UPDATE series SET cadence_day = $day, cadence_start = $start WHERE id = $id;";
        command.Parameters.AddWithValue("$day", (object?)cadenceDay ?? DBNull.Value);
        command.Parameters.AddWithValue("$start", (object?)cadenceStart ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", seriesId);
        command.ExecuteNonQuery();
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

    /// <summary>
    /// Full-row update of the editable schedule entry columns (series_id, position, week,
    /// publish_date, title, tags, notes). Does not touch published, published_on, topic,
    /// source, or draft_filename. May throw a SqliteException on the UNIQUE(series_id,
    /// position) constraint if the target position is already occupied in the target
    /// series — callers that need to move an entry into an occupied slot should
    /// orchestrate the move via <see cref="Renumber"/> instead.
    /// </summary>
    public void UpdateEntry(long entryId, long seriesId, int position, int? week,
        DateOnly? publishDate, string title, string? tags, string? notes)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            UPDATE schedule_entries
            SET series_id = $series, position = $position, week = $week,
                publish_date = $date, title = $title, tags = $tags, notes = $notes
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$series", seriesId);
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$week", (object?)week ?? DBNull.Value);
        command.Parameters.AddWithValue("$date",
            (object?)publishDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$tags", (object?)tags ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", entryId);
        command.ExecuteNonQuery();
    }

    public void DeleteEntry(long entryId)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "DELETE FROM schedule_entries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", entryId);
        command.ExecuteNonQuery();
    }

    public int CountEntries(long seriesId)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schedule_entries WHERE series_id = $series;";
        command.Parameters.AddWithValue("$series", seriesId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>
    /// Reassigns positions for the given entries within a series. Runs in a transaction
    /// and uses a two-pass update (each entry first moved to the negative of its target
    /// position, then to the final positive value) to avoid tripping the
    /// UNIQUE(series_id, position) constraint when target positions overlap with
    /// current ones (e.g. swapping two entries). Only entries present in <paramref
    /// name="order"/> are touched.
    /// </summary>
    public void Renumber(long seriesId, IReadOnlyList<(long EntryId, int Position)> order)
    {
        using var transaction = Connection.BeginTransaction();

        foreach (var (entryId, position) in order)
        {
            using var command = Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "UPDATE schedule_entries SET position = $position WHERE id = $id AND series_id = $series;";
            command.Parameters.AddWithValue("$position", -position);
            command.Parameters.AddWithValue("$id", entryId);
            command.Parameters.AddWithValue("$series", seriesId);
            command.ExecuteNonQuery();
        }

        foreach (var (entryId, position) in order)
        {
            using var command = Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "UPDATE schedule_entries SET position = $position WHERE id = $id AND series_id = $series;";
            command.Parameters.AddWithValue("$position", position);
            command.Parameters.AddWithValue("$id", entryId);
            command.Parameters.AddWithValue("$series", seriesId);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// Shifts the publish_date of every unpublished, dated entry in the series by
    /// <paramref name="deltaDays"/> days (negative shifts earlier) as a single UPDATE,
    /// so the whole move is atomic. Published entries and entries without a publish_date
    /// are untouched. Returns the number of rows moved.
    /// </summary>
    public int ShiftUnpublishedEntryDates(long seriesId, int deltaDays)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            UPDATE schedule_entries
            SET publish_date = date(publish_date, $delta)
            WHERE series_id = $series AND published = 0 AND publish_date IS NOT NULL;
            """;
        command.Parameters.AddWithValue("$delta", $"{deltaDays} days");
        command.Parameters.AddWithValue("$series", seriesId);
        return command.ExecuteNonQuery();
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
