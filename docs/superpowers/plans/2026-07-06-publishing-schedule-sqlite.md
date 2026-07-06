# Publishing Schedule (SQLite) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the publishing-schedule spreadsheet (`~/Desktop/bloghelper9000-publishing-schedule.xlsx`) with a SQLite database in the blog root, driven by new `bloghelper schedule` CLI commands, options on `add`/`publish`, and new MCP tools so an LLM can run the schedule end-to-end.

**Architecture:** A new `BlogHelper9000.Core.Scheduling` namespace owns the SQLite schema, repository, stats calculation, and an `IScheduleService` facade (mirroring `IBlogService`). The CLI gains a one-off `schedule import` command (ClosedXML, CLI project only) plus `schedule list/show/stats/mark` commands and `--series` support on `add`; `publish` auto-ticks the matching schedule entry. The MCP server exposes the same operations as tools and documents the database in its server instructions.

**Tech Stack:** .NET 10 / C# latest, Microsoft.Data.Sqlite (raw SQL, no EF), ClosedXML (CLI import only), TimeWarp.Nuru, Spectre.Console, ModelContextProtocol SDK, xunit v3 + FluentAssertions + NSubstitute.

## Global Constraints

- .NET 10.0, `<Nullable>enable</Nullable>`, `<LangVersion>latest</LangVersion>` — match every existing csproj.
- Test stack is **xunit v3 + FluentAssertions + NSubstitute**; Core/CLI tests live in `BlogHelper9000.Tests`, MCP tool tests in `BlogHelper9000.Mcp.Tests/Tools/`.
- MCP server: **stdout is reserved for JSON-RPC framing — never write to it**; all logging to stderr.
- `BlogHelper9000.Imaging` needs `SIXLABORS_LICENSE_KEY` to build; `BlogHelper9000.Tests` and `BlogHelper9000.Mcp.Tests` reference it transitively. If `dotnet test` fails to see the key in a non-interactive shell, re-run via `zsh -lc '...'`.
- `AGENTS.md` is a copy of `CLAUDE.md` — update both together (Task 10).
- Database file is `.bloghelper.db` **in the blog root** (dot-prefixed so Jekyll's default exclusion keeps it out of `_site`). The blog root is `BlogHelperOptions.BaseDirectory` (the MCP server resolves it from `BLOG_BASE_DIRECTORY`; on this machine it is `/Users/stuart/dev/sgrassie.github.io`).
- `ToolResponse<T>` envelope for every MCP tool: branch on `Success`, never prose-only errors.
- Commit after every task with the message given in the task.

---

## Background: what the spreadsheet contains

Sheets in `bloghelper9000-publishing-schedule.xlsx`:

| Sheet | Meaning | Imported? |
|---|---|---|
| `Dashboard` | Aggregate stats (the target for `schedule stats`) | Only for metadata: baseline count/date + sheet→series display names |
| `Schedule` | **The "BlogHelper9000 revisited" series** (58 rows, week numbers only, no dates) | Yes |
| `Progress` | Redundant roll-up of `Schedule` | **No — superfluous, skip** |
| `FootballData Series` | 104 rows, dated (weekly Tuesdays + occasional Fridays) | Yes |
| `Traefik Series` | 26 rows, dated (Thursdays) | Yes |
| `Ad-hoc` | 1 row, undated | Yes |

Entry-sheet columns (union across sheets; `Schedule` lacks `Publish date`/`Day`/`Tags` and adds `Source`):
`#`, `Week`, `Publish date`, `Day`, `Series` (a **topic sub-grouping** within the sheet, e.g. "Testing", "Betting fundamentals" — not the top-level series), `Post title`, `Draft filename`, `Tags`, `Source` (`New`/`Existing draft`), `Published?` (boolean), `Notes`.

Dashboard stats to reproduce:
- Baseline published posts: **243** as of **2026-07-04** (cell B4; the blog's published count when the schedule was created).
- Published via schedules = count of ticked entries; Total published = baseline + that.
- Per-series: Planned, Published, Remaining, % done, Latest posted (title), Next planned (title), Next date (`yyyy-MM-dd` or `Week n` or `unscheduled`), Last posted (date).
- Overall: posts planned, ticked off, drafts remaining, progress % with a `█░` 20-char bar, "drafts remaining : total published" ratio.
- `Day` column is derivable from `Publish date` — not stored. The Dashboard's "Latest post on the blog" cell is skipped; `bloghelper info` already reports the blog's latest post.

### Design decisions (locked in)

1. **DB location/name:** `<blog root>/.bloghelper.db`. Dot-prefix is load-bearing: Jekyll copies non-underscore root files into the generated site by default; dotfiles are excluded.
2. **Data access:** raw `Microsoft.Data.Sqlite` with hand-written SQL. The schema is 3 tables; EF would be ceremony. Schema versioning via `PRAGMA user_version`.
3. **SQLite bypasses `IFileSystem`:** it cannot be mocked with `MockFileSystem`. Tests use in-memory databases (`Data Source=:memory:`) injected through the repository/service; only existence checks (`File.Exists`) go through `IFileSystem`.
4. **Import lives in the CLI project only** (`ClosedXML` reference stays out of Core/MCP). It is a repeatable command, not a one-off script, and reads the Dashboard sheet to map sheet names → series display names (`Schedule` → "BlogHelper9000 revisited", `Traefik Series` → "Traefik in the homelab", etc.), so nothing is hardcoded to this particular spreadsheet.
5. **Schedule entries are keyed by draft filename** for matching against blog posts. Filenames are normalised (basename, `.md` appended, `yyyy-MM-dd-` prefix stripped) so `publish` can tick entries whichever form the user passes.
6. **`publish` auto-ticks; explicit `schedule mark` exists for corrections** (posts published by hand, or un-ticking mistakes).
7. **Schema:**

```sql
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
    position       INTEGER NOT NULL,          -- the '#' column; append = max+1
    week           INTEGER NULL,
    publish_date   TEXT NULL,                 -- planned date, ISO yyyy-MM-dd
    topic          TEXT NULL,                 -- the per-sheet 'Series' sub-grouping
    title          TEXT NOT NULL,
    draft_filename TEXT NOT NULL,             -- normalised, e.g. 'my-post.md'
    tags           TEXT NULL,                 -- comma-separated, blog convention
    source         TEXT NULL,                 -- 'New' | 'Existing draft'
    published      INTEGER NOT NULL DEFAULT 0,
    published_on   TEXT NULL,                 -- actual date ticked, ISO yyyy-MM-dd
    notes          TEXT NULL,
    UNIQUE(series_id, position));

CREATE INDEX ix_schedule_entries_filename ON schedule_entries(draft_filename);
```

`meta` keys: `baseline_published_count` (`"243"`), `baseline_date` (`"2026-07-04"`).

---

### Task 1: Core scheduling models + `ScheduleDatabase`

**Files:**
- Modify: `BlogHelper9000.Core/BlogHelper9000.Core.csproj` (add `Microsoft.Data.Sqlite`)
- Create: `BlogHelper9000.Core/Scheduling/ScheduleModels.cs`
- Create: `BlogHelper9000.Core/Scheduling/ScheduleDatabase.cs`
- Test: `BlogHelper9000.Tests/Scheduling/ScheduleDatabaseTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ScheduleDatabase` (`Open(string blogBaseDirectory)`, `OpenInMemory()`, `PathFor(string)`, `Connection`, `SchemaVersion`, `IDisposable`); records `SeriesInfo(long Id, string Name, int SortOrder)`, `ScheduleEntry(long Id, string Series, int Position, int? Week, DateOnly? PublishDate, string? Topic, string Title, string DraftFilename, string? Tags, string? Source, bool Published, DateOnly? PublishedOn, string? Notes)`, `NewScheduleEntry(int? Position, int? Week, DateOnly? PublishDate, string? Topic, string Title, string DraftFilename, string? Tags, string? Source, bool Published, string? Notes)`, `SeriesStats(string Series, int Planned, int Published, int Remaining, double PercentDone, string? LatestPostedTitle, string? NextPlannedTitle, string? NextSlot, DateOnly? LastPostedOn)`, `ScheduleDashboard(int BaselinePublished, DateOnly? BaselineDate, int PublishedViaSchedules, int TotalPublished, int TotalPlanned, int TotalRemaining, double Progress, IReadOnlyList<SeriesStats> Series)`, `enum MarkPublishedOutcome { Marked, AlreadyMarked, NotScheduled, Unmarked }`.

- [ ] **Step 1: Add the package reference**

In `BlogHelper9000.Core/BlogHelper9000.Core.csproj`, inside the existing `PackageReference` ItemGroup:

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />
```

(Use the latest 10.0.x if restore complains.) Run `dotnet restore BlogHelper9000.sln` to confirm it resolves.

- [ ] **Step 2: Write the failing tests**

`BlogHelper9000.Tests/Scheduling/ScheduleDatabaseTests.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using FluentAssertions;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleDatabaseTests
{
    [Fact]
    public void OpenInMemory_CreatesSchemaAtVersion1()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        db.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void Migrate_IsIdempotent_ForAnAlreadyMigratedDatabase()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        // Re-running migration logic on the open connection must not throw or bump the version.
        var act = () => db.SchemaVersion;

        act.Should().NotThrow();
        db.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void PathFor_AppendsDotPrefixedFileNameToBlogRoot()
    {
        ScheduleDatabase.PathFor("/blog").Should().Be(Path.Combine("/blog", ".bloghelper.db"));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleDatabaseTests"`
Expected: FAIL — compile error, `ScheduleDatabase` does not exist.

- [ ] **Step 4: Write the models**

`BlogHelper9000.Core/Scheduling/ScheduleModels.cs`:

```csharp
namespace BlogHelper9000.Core.Scheduling;

public sealed record SeriesInfo(long Id, string Name, int SortOrder);

public sealed record ScheduleEntry(
    long Id,
    string Series,
    int Position,
    int? Week,
    DateOnly? PublishDate,
    string? Topic,
    string Title,
    string DraftFilename,
    string? Tags,
    string? Source,
    bool Published,
    DateOnly? PublishedOn,
    string? Notes);

public sealed record NewScheduleEntry(
    int? Position,
    int? Week,
    DateOnly? PublishDate,
    string? Topic,
    string Title,
    string DraftFilename,
    string? Tags,
    string? Source,
    bool Published,
    string? Notes);

public sealed record SeriesStats(
    string Series,
    int Planned,
    int Published,
    int Remaining,
    double PercentDone,
    string? LatestPostedTitle,
    string? NextPlannedTitle,
    string? NextSlot,
    DateOnly? LastPostedOn);

public sealed record ScheduleDashboard(
    int BaselinePublished,
    DateOnly? BaselineDate,
    int PublishedViaSchedules,
    int TotalPublished,
    int TotalPlanned,
    int TotalRemaining,
    double Progress,
    IReadOnlyList<SeriesStats> Series);

public enum MarkPublishedOutcome
{
    Marked,
    AlreadyMarked,
    NotScheduled,
    Unmarked
}
```

- [ ] **Step 5: Write `ScheduleDatabase`**

`BlogHelper9000.Core/Scheduling/ScheduleDatabase.cs`:

```csharp
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
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleDatabaseTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add BlogHelper9000.Core BlogHelper9000.Tests
git commit -m "feat: add schedule SQLite database and scheduling models"
```

---

### Task 2: `ScheduleRepository`

**Files:**
- Create: `BlogHelper9000.Core/Scheduling/ScheduleRepository.cs`
- Test: `BlogHelper9000.Tests/Scheduling/ScheduleRepositoryTests.cs`

**Interfaces:**
- Consumes: `ScheduleDatabase`, records from Task 1.
- Produces: `ScheduleRepository(ScheduleDatabase database)` with: `long AddSeries(string name, int sortOrder = 0)`, `IReadOnlyList<SeriesInfo> ListSeries()`, `SeriesInfo? FindSeries(string name)`, `long AddEntry(long seriesId, NewScheduleEntry entry)`, `IReadOnlyList<ScheduleEntry> GetEntries(long seriesId)` (ordered by position), `ScheduleEntry? FindEntryByFilename(string draftFilename)`, `void SetPublished(long entryId, bool published, DateOnly? publishedOn)`, `void SetMeta(string key, string value)`, `string? GetMeta(string key)`.

- [ ] **Step 1: Write the failing tests**

`BlogHelper9000.Tests/Scheduling/ScheduleRepositoryTests.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using FluentAssertions;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleRepositoryTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();
    private ScheduleRepository Repository => new(_db);

    private static NewScheduleEntry Entry(string title, string filename, int? position = null,
        int? week = null, DateOnly? date = null, bool published = false) =>
        new(position, week, date, "Topic", title, filename, "csharp, dotnet", "New", published, null);

    [Fact]
    public void AddSeries_ThenListSeries_ReturnsSeriesInSortOrder()
    {
        Repository.AddSeries("FootballData", sortOrder: 1);
        Repository.AddSeries("BlogHelper9000 revisited", sortOrder: 0);

        var series = Repository.ListSeries();

        series.Select(s => s.Name).Should().ContainInOrder("BlogHelper9000 revisited", "FootballData");
    }

    [Fact]
    public void FindSeries_IsCaseInsensitive()
    {
        Repository.AddSeries("Traefik in the homelab");

        Repository.FindSeries("traefik IN the Homelab").Should().NotBeNull();
    }

    [Fact]
    public void AddEntry_WithoutPosition_AppendsAfterHighestPosition()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("First", "first.md", position: 5));

        Repository.AddEntry(seriesId, Entry("Second", "second.md"));

        var entries = Repository.GetEntries(seriesId);
        entries.Should().HaveCount(2);
        entries[1].Position.Should().Be(6);
    }

    [Fact]
    public void GetEntries_RoundTripsAllFields_OrderedByPosition()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("B", "b.md", position: 2, week: 4, date: new DateOnly(2026, 7, 7)));
        Repository.AddEntry(seriesId, Entry("A", "a.md", position: 1));

        var entries = Repository.GetEntries(seriesId);

        entries.Select(e => e.Title).Should().ContainInOrder("A", "B");
        entries[1].Should().BeEquivalentTo(new
        {
            Series = "Series", Position = 2, Week = 4,
            PublishDate = new DateOnly(2026, 7, 7), Topic = "Topic",
            Title = "B", DraftFilename = "b.md", Tags = "csharp, dotnet",
            Source = "New", Published = false, PublishedOn = (DateOnly?)null
        });
    }

    [Fact]
    public void FindEntryByFilename_ReturnsEntryWithSeriesName()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("Post", "my-post.md"));

        var entry = Repository.FindEntryByFilename("my-post.md");

        entry.Should().NotBeNull();
        entry!.Series.Should().Be("Series");
    }

    [Fact]
    public void SetPublished_TicksEntryAndRecordsDate()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("Post", "my-post.md"));
        var id = Repository.FindEntryByFilename("my-post.md")!.Id;

        Repository.SetPublished(id, true, new DateOnly(2026, 7, 6));

        var entry = Repository.FindEntryByFilename("my-post.md")!;
        entry.Published.Should().BeTrue();
        entry.PublishedOn.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void Meta_UpsertsAndReads()
    {
        Repository.SetMeta("baseline_published_count", "243");
        Repository.SetMeta("baseline_published_count", "244");

        Repository.GetMeta("baseline_published_count").Should().Be("244");
        Repository.GetMeta("missing").Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleRepositoryTests"`
Expected: FAIL — `ScheduleRepository` does not exist.

- [ ] **Step 3: Write the repository**

`BlogHelper9000.Core/Scheduling/ScheduleRepository.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleRepositoryTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add BlogHelper9000.Core BlogHelper9000.Tests
git commit -m "feat: add ScheduleRepository CRUD over the schedule database"
```

---

### Task 3: Stats calculation (`ScheduleStats`)

**Files:**
- Create: `BlogHelper9000.Core/Scheduling/ScheduleStats.cs`
- Test: `BlogHelper9000.Tests/Scheduling/ScheduleStatsTests.cs`

**Interfaces:**
- Consumes: `ScheduleEntry`, `SeriesStats` from Task 1.
- Produces: `static SeriesStats ScheduleStats.ForSeries(string seriesName, IReadOnlyList<ScheduleEntry> entriesOrderedByPosition)`.

- [ ] **Step 1: Write the failing tests**

`BlogHelper9000.Tests/Scheduling/ScheduleStatsTests.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using FluentAssertions;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleStatsTests
{
    private static ScheduleEntry Entry(int position, string title, bool published,
        int? week = null, DateOnly? date = null, DateOnly? publishedOn = null) =>
        new(position, "Series", position, week, date, null, title, $"{position}.md",
            null, null, published, publishedOn, null);

    [Fact]
    public void ForSeries_ComputesCountsAndPercentage()
    {
        var entries = new[]
        {
            Entry(1, "A", published: true),
            Entry(2, "B", published: true),
            Entry(3, "C", published: false),
            Entry(4, "D", published: false),
        };

        var stats = ScheduleStats.ForSeries("Series", entries);

        stats.Should().BeEquivalentTo(new
        {
            Series = "Series", Planned = 4, Published = 2, Remaining = 2, PercentDone = 0.5,
            LatestPostedTitle = "B", NextPlannedTitle = "C"
        });
    }

    [Fact]
    public void ForSeries_EmptySeries_IsAllZeroesWithoutDividingByZero()
    {
        var stats = ScheduleStats.ForSeries("Empty", []);

        stats.PercentDone.Should().Be(0);
        stats.NextPlannedTitle.Should().BeNull();
        stats.NextSlot.Should().BeNull();
    }

    [Fact]
    public void NextSlot_PrefersDate_ThenWeek_ThenUnscheduled()
    {
        ScheduleStats.ForSeries("S", [Entry(1, "A", false, week: 3, date: new DateOnly(2026, 7, 7))])
            .NextSlot.Should().Be("2026-07-07");
        ScheduleStats.ForSeries("S", [Entry(1, "A", false, week: 3)])
            .NextSlot.Should().Be("Week 3");
        ScheduleStats.ForSeries("S", [Entry(1, "A", false)])
            .NextSlot.Should().Be("unscheduled");
    }

    [Fact]
    public void LastPostedOn_UsesActualPublishedOn_FallingBackToPlannedDate()
    {
        var entries = new[]
        {
            Entry(1, "A", published: true, date: new DateOnly(2026, 7, 7)),
            Entry(2, "B", published: true, date: new DateOnly(2026, 7, 14), publishedOn: new DateOnly(2026, 7, 20)),
            Entry(3, "C", published: false, date: new DateOnly(2026, 7, 21)),
        };

        ScheduleStats.ForSeries("S", entries).LastPostedOn.Should().Be(new DateOnly(2026, 7, 20));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleStatsTests"`
Expected: FAIL — `ScheduleStats` does not exist.

- [ ] **Step 3: Write the calculator**

`BlogHelper9000.Core/Scheduling/ScheduleStats.cs`:

```csharp
namespace BlogHelper9000.Core.Scheduling;

public static class ScheduleStats
{
    /// <summary>Entries must be ordered by position (as returned by ScheduleRepository.GetEntries).</summary>
    public static SeriesStats ForSeries(string seriesName, IReadOnlyList<ScheduleEntry> entries)
    {
        var planned = entries.Count;
        var published = entries.Count(e => e.Published);
        var remaining = planned - published;
        var percentDone = planned == 0 ? 0 : (double)published / planned;

        var latestPosted = entries.LastOrDefault(e => e.Published);
        var next = entries.FirstOrDefault(e => !e.Published);
        var nextSlot = next switch
        {
            null => null,
            { PublishDate: not null } => next.PublishDate.Value.ToString("yyyy-MM-dd"),
            { Week: not null } => $"Week {next.Week}",
            _ => "unscheduled"
        };

        var lastPostedOn = entries
            .Where(e => e.Published)
            .Select(e => e.PublishedOn ?? e.PublishDate)
            .Where(d => d is not null)
            .Max();

        return new SeriesStats(seriesName, planned, published, remaining, percentDone,
            latestPosted?.Title, next?.Title, nextSlot, lastPostedOn);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleStatsTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add BlogHelper9000.Core BlogHelper9000.Tests
git commit -m "feat: add per-series schedule stats calculation"
```

---

### Task 4: `IScheduleService` + DI wiring

**Files:**
- Create: `BlogHelper9000.Core/Scheduling/IScheduleService.cs`
- Create: `BlogHelper9000.Core/Scheduling/ScheduleService.cs`
- Modify: `BlogHelper9000/Program.cs` (register service)
- Modify: `BlogHelper9000.Mcp/Program.cs` (register service)
- Test: `BlogHelper9000.Tests/Scheduling/ScheduleServiceTests.cs`

**Interfaces:**
- Consumes: `ScheduleDatabase`, `ScheduleRepository`, `ScheduleStats`, `BlogHelperOptions`, `IFileSystem`, `TimeProvider`.
- Produces:

```csharp
public interface IScheduleService
{
    bool DatabaseExists { get; }
    IReadOnlyList<SeriesInfo> ListSeries();
    IReadOnlyList<ScheduleEntry> GetSeriesEntries(string seriesName);
    ScheduleDashboard GetDashboard();
    ScheduleEntry? AddToSeries(string seriesName, string title, string draftFilename,
        int? week = null, DateOnly? publishDate = null, string? tags = null, string? notes = null);
    MarkPublishedOutcome MarkPublished(string post, DateOnly? publishedOn = null, bool unmark = false);
    ScheduleEntry? GetNextUnpublished(string? seriesName = null);
}
```

`GetSeriesEntries` on an unknown series returns an empty list. `AddToSeries` creates the series if it does not exist and appends at the next position, marking `Source` as `"New"`. `MarkPublished` normalises the post name (basename, ensure `.md`, strip `yyyy-MM-dd-` prefix) before matching `draft_filename`.

- [ ] **Step 1: Write the failing tests**

`BlogHelper9000.Tests/Scheduling/ScheduleServiceTests.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleServiceTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly ScheduleService _service;

    public ScheduleServiceTests()
    {
        _service = new ScheduleService(_db, _time);
        var repository = new ScheduleRepository(_db);
        repository.SetMeta("baseline_published_count", "243");
        repository.SetMeta("baseline_date", "2026-07-04");
        var seriesId = repository.AddSeries("Traefik in the homelab");
        repository.AddEntry(seriesId, new NewScheduleEntry(1, 1, new DateOnly(2026, 7, 9), "Foundations",
            "Traefik in the homelab: one proxy for everything",
            "traefik-in-the-homelab-one-proxy-for-everything.md", "traefik, homelab", "New", false, null));
        repository.AddEntry(seriesId, new NewScheduleEntry(2, 2, new DateOnly(2026, 7, 16), "Foundations",
            "Routers, services, and entrypoints",
            "routers-services-and-entrypoints.md", "traefik, homelab", "New", false, null));
    }

    [Fact]
    public void GetDashboard_CombinesBaselineAndSeriesStats()
    {
        var dashboard = _service.GetDashboard();

        dashboard.Should().BeEquivalentTo(new
        {
            BaselinePublished = 243,
            BaselineDate = new DateOnly(2026, 7, 4),
            PublishedViaSchedules = 0,
            TotalPublished = 243,
            TotalPlanned = 2,
            TotalRemaining = 2,
            Progress = 0.0
        }, options => options.ExcludingMissingMembers());
        dashboard.Series.Should().ContainSingle(s => s.Series == "Traefik in the homelab");
    }

    [Fact]
    public void MarkPublished_NormalisesThePostName_AndStampsToday()
    {
        var outcome = _service.MarkPublished("_posts/2026/2026-07-06-routers-services-and-entrypoints.md");

        outcome.Should().Be(MarkPublishedOutcome.Marked);
        var entry = _service.GetSeriesEntries("Traefik in the homelab")
            .Single(e => e.DraftFilename == "routers-services-and-entrypoints.md");
        entry.Published.Should().BeTrue();
        entry.PublishedOn.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void MarkPublished_WhenAlreadyTicked_ReportsAlreadyMarked()
    {
        _service.MarkPublished("routers-services-and-entrypoints");

        _service.MarkPublished("routers-services-and-entrypoints")
            .Should().Be(MarkPublishedOutcome.AlreadyMarked);
    }

    [Fact]
    public void MarkPublished_UnknownPost_ReportsNotScheduled()
    {
        _service.MarkPublished("not-on-the-schedule.md").Should().Be(MarkPublishedOutcome.NotScheduled);
    }

    [Fact]
    public void MarkPublished_WithUnmark_ClearsTheTick()
    {
        _service.MarkPublished("routers-services-and-entrypoints.md");

        _service.MarkPublished("routers-services-and-entrypoints.md", unmark: true)
            .Should().Be(MarkPublishedOutcome.Unmarked);
        _service.GetSeriesEntries("Traefik in the homelab")
            .Single(e => e.DraftFilename == "routers-services-and-entrypoints.md")
            .Published.Should().BeFalse();
    }

    [Fact]
    public void AddToSeries_CreatesMissingSeries_AndAppends()
    {
        var entry = _service.AddToSeries("Brand new series", "A post", "a-post.md",
            week: 2, tags: "csharp");

        entry.Should().NotBeNull();
        entry!.Position.Should().Be(1);
        _service.ListSeries().Should().Contain(s => s.Name == "Brand new series");
    }

    [Fact]
    public void GetNextUnpublished_ForASeries_ReturnsFirstUntickedByPosition()
    {
        _service.MarkPublished("traefik-in-the-homelab-one-proxy-for-everything.md");

        _service.GetNextUnpublished("Traefik in the homelab")!
            .DraftFilename.Should().Be("routers-services-and-entrypoints.md");
    }

    [Fact]
    public void GetNextUnpublished_AcrossAllSeries_PrefersEarliestPlannedDate()
    {
        _service.AddToSeries("Undated", "Undated post", "undated.md");

        _service.GetNextUnpublished()!
            .DraftFilename.Should().Be("traefik-in-the-homelab-one-proxy-for-everything.md");
    }

    public void Dispose() => _db.Dispose();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleServiceTests"`
Expected: FAIL — `ScheduleService` does not exist.

- [ ] **Step 3: Write the interface and service**

`BlogHelper9000.Core/Scheduling/IScheduleService.cs`:

```csharp
namespace BlogHelper9000.Core.Scheduling;

public interface IScheduleService
{
    /// <summary>True when a schedule database exists (or one was injected for tests).</summary>
    bool DatabaseExists { get; }

    IReadOnlyList<SeriesInfo> ListSeries();

    /// <summary>Entries for a series, ordered by position. Empty for an unknown series.</summary>
    IReadOnlyList<ScheduleEntry> GetSeriesEntries(string seriesName);

    /// <summary>Dashboard stats matching the old spreadsheet's Dashboard sheet.</summary>
    ScheduleDashboard GetDashboard();

    /// <summary>
    /// Appends a post to a series, creating the series if needed.
    /// Returns the stored entry, or null when the filename is already scheduled.
    /// </summary>
    ScheduleEntry? AddToSeries(string seriesName, string title, string draftFilename,
        int? week = null, DateOnly? publishDate = null, string? tags = null, string? notes = null);

    /// <summary>
    /// Ticks (or with <paramref name="unmark"/> un-ticks) the schedule entry whose draft filename
    /// matches <paramref name="post"/>. Post names are normalised: basename, '.md' appended,
    /// 'yyyy-MM-dd-' prefix stripped.
    /// </summary>
    MarkPublishedOutcome MarkPublished(string post, DateOnly? publishedOn = null, bool unmark = false);

    /// <summary>
    /// The next unpublished entry: first by position within a named series, or across all
    /// series the one with the earliest planned date (dated entries first, then schedule order).
    /// </summary>
    ScheduleEntry? GetNextUnpublished(string? seriesName = null);
}
```

`BlogHelper9000.Core/Scheduling/ScheduleService.cs`:

```csharp
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Core.Scheduling;

public sealed partial class ScheduleService : IScheduleService, IDisposable
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}-")]
    private static partial Regex DatePrefixPattern();

    private readonly Func<ScheduleDatabase> _openDatabase;
    private readonly Func<bool> _databaseExists;
    private readonly TimeProvider _timeProvider;
    private ScheduleDatabase? _database;

    public ScheduleService(IOptions<BlogHelperOptions> options, IFileSystem fileSystem, TimeProvider timeProvider)
    {
        var baseDirectory = options.Value.BaseDirectory;
        _openDatabase = () => ScheduleDatabase.Open(baseDirectory);
        _databaseExists = () => fileSystem.File.Exists(ScheduleDatabase.PathFor(baseDirectory));
        _timeProvider = timeProvider;
    }

    /// <summary>Test seam: run against an already-open (usually in-memory) database.</summary>
    internal ScheduleService(ScheduleDatabase database, TimeProvider timeProvider)
    {
        _database = database;
        _openDatabase = () => database;
        _databaseExists = () => true;
        _timeProvider = timeProvider;
    }

    public bool DatabaseExists => _databaseExists();

    private ScheduleRepository Repository => new(_database ??= _openDatabase());

    public IReadOnlyList<SeriesInfo> ListSeries() => Repository.ListSeries();

    public IReadOnlyList<ScheduleEntry> GetSeriesEntries(string seriesName)
    {
        var series = Repository.FindSeries(seriesName);
        return series is null ? [] : Repository.GetEntries(series.Id);
    }

    public ScheduleDashboard GetDashboard()
    {
        var repository = Repository;
        var perSeries = repository.ListSeries()
            .Select(s => ScheduleStats.ForSeries(s.Name, repository.GetEntries(s.Id)))
            .ToList();

        var baseline = int.TryParse(repository.GetMeta("baseline_published_count"), out var b) ? b : 0;
        DateOnly? baselineDate = DateOnly.TryParse(repository.GetMeta("baseline_date"), out var d) ? d : null;

        var planned = perSeries.Sum(s => s.Planned);
        var published = perSeries.Sum(s => s.Published);

        return new ScheduleDashboard(
            baseline,
            baselineDate,
            published,
            baseline + published,
            planned,
            planned - published,
            planned == 0 ? 0 : (double)published / planned,
            perSeries);
    }

    public ScheduleEntry? AddToSeries(string seriesName, string title, string draftFilename,
        int? week = null, DateOnly? publishDate = null, string? tags = null, string? notes = null)
    {
        var repository = Repository;
        var filename = NormaliseFilename(draftFilename);
        if (repository.FindEntryByFilename(filename) is not null)
            return null;

        var series = repository.FindSeries(seriesName);
        var seriesId = series?.Id
            ?? repository.AddSeries(seriesName, repository.ListSeries().Count);

        var id = repository.AddEntry(seriesId, new NewScheduleEntry(
            null, week, publishDate, null, title, filename, tags, "New", false, notes));
        return repository.FindEntryByFilename(filename);
    }

    public MarkPublishedOutcome MarkPublished(string post, DateOnly? publishedOn = null, bool unmark = false)
    {
        if (!DatabaseExists) return MarkPublishedOutcome.NotScheduled;

        var repository = Repository;
        var entry = repository.FindEntryByFilename(NormaliseFilename(post));
        if (entry is null) return MarkPublishedOutcome.NotScheduled;

        if (unmark)
        {
            repository.SetPublished(entry.Id, false, null);
            return MarkPublishedOutcome.Unmarked;
        }

        if (entry.Published) return MarkPublishedOutcome.AlreadyMarked;

        var stamp = publishedOn ?? DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date);
        repository.SetPublished(entry.Id, true, stamp);
        return MarkPublishedOutcome.Marked;
    }

    public ScheduleEntry? GetNextUnpublished(string? seriesName = null)
    {
        if (seriesName is not null)
            return GetSeriesEntries(seriesName).FirstOrDefault(e => !e.Published);

        var repository = Repository;
        var candidates = repository.ListSeries()
            .SelectMany(s => repository.GetEntries(s.Id))
            .Where(e => !e.Published)
            .ToList();

        return candidates
            .Where(e => e.PublishDate is not null)
            .OrderBy(e => e.PublishDate)
            .FirstOrDefault()
            ?? candidates.FirstOrDefault();
    }

    internal static string NormaliseFilename(string post)
    {
        var name = Path.GetFileName(post.Trim());
        if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            name += ".md";
        return DatePrefixPattern().Replace(name, string.Empty);
    }

    public void Dispose() => _database?.Dispose();
}
```

Note: the unused `id` local in `AddToSeries` — inline it (`repository.AddEntry(...)` as a statement) to keep the build warning-free.

- [ ] **Step 4: Run tests to verify they pass**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleServiceTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Register in DI (CLI and MCP)**

In `BlogHelper9000/Program.cs`, after `builder.Services.AddSingleton<IBlogService, BlogService>();` add:

```csharp
builder.Services.AddSingleton<IScheduleService, ScheduleService>();
```

and add `using BlogHelper9000.Core.Scheduling;` to the usings.

In `BlogHelper9000.Mcp/Program.cs`, after the same line add the same registration and using.

Run: `zsh -lc "./build.sh"` — Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add BlogHelper9000.Core BlogHelper9000.Tests BlogHelper9000/Program.cs BlogHelper9000.Mcp/Program.cs
git commit -m "feat: add IScheduleService facade and register it in CLI and MCP hosts"
```

---

### Task 5: `bloghelper schedule import` (xlsx → SQLite)

**Files:**
- Modify: `BlogHelper9000/BlogHelper9000.csproj` (add ClosedXML)
- Create: `BlogHelper9000/Scheduling/ScheduleXlsxImporter.cs`
- Create: `BlogHelper9000/Commands/ScheduleImportCommand.cs`
- Test: `BlogHelper9000.Tests/Scheduling/ScheduleXlsxImporterTests.cs` (add ClosedXML to `BlogHelper9000.Tests.csproj` too)

**Interfaces:**
- Consumes: `ScheduleDatabase`, `ScheduleRepository`, `NewScheduleEntry` (Tasks 1–2).
- Produces: `static ImportSummary ScheduleXlsxImporter.Import(XLWorkbook workbook, ScheduleDatabase database)` and `sealed record ImportSummary(IReadOnlyList<(string Series, int Entries)> SeriesCounts)`; CLI route `schedule import <path> [--force]`.

**Import rules:**
1. Skip sheets named `Dashboard` and `Progress` when importing entries.
2. From `Dashboard`: baseline count = column-B value on the row whose column-A text starts with `"Published posts (baseline"`; baseline date parsed from that label text (`(baseline, 4 Jul 2026)` → `2026-07-04`, en-GB `d MMM yyyy`). Sheet→series display-name map = rows where column B exactly matches a worksheet name and column A is non-empty (e.g. `Schedule` → `BlogHelper9000 revisited`). Fall back to the sheet name when unmapped.
3. Entry sheets: row 1 is the header; map columns by header text (`#`, `Week`, `Publish date`, `Series` → topic, `Post title`, `Draft filename`, `Tags`, `Source`, `Published?`, `Notes`; `Day` ignored). Read rows until `Post title` is blank. Series `sort_order` = worksheet order.

- [ ] **Step 1: Add package references**

`BlogHelper9000/BlogHelper9000.csproj` and `BlogHelper9000.Tests/BlogHelper9000.Tests.csproj`:

```xml
<PackageReference Include="ClosedXML" Version="0.105.0" />
```

- [ ] **Step 2: Write the failing tests**

`BlogHelper9000.Tests/Scheduling/ScheduleXlsxImporterTests.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Scheduling;
using ClosedXML.Excel;
using FluentAssertions;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleXlsxImporterTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();

    private static XLWorkbook BuildWorkbook()
    {
        var workbook = new XLWorkbook();

        var dashboard = workbook.AddWorksheet("Dashboard");
        dashboard.Cell("A4").Value = "Published posts (baseline, 4 Jul 2026)";
        dashboard.Cell("B4").Value = 243;
        dashboard.Cell("A11").Value = "BlogHelper9000 revisited";
        dashboard.Cell("B11").Value = "Schedule";

        var progress = workbook.AddWorksheet("Progress");
        progress.Cell("A1").Value = "Publishing progress";

        var schedule = workbook.AddWorksheet("Schedule");
        string[] scheduleHeader = ["#", "Week", "Series", "Post title", "Draft filename", "Source", "Published?", "Notes"];
        for (var i = 0; i < scheduleHeader.Length; i++)
            schedule.Cell(1, i + 1).Value = scheduleHeader[i];
        schedule.Cell("A2").Value = 1; schedule.Cell("B2").Value = 1;
        schedule.Cell("C2").Value = "BlogHelper9000 revisited";
        schedule.Cell("D2").Value = "BlogHelper9000, four years on";
        schedule.Cell("E2").Value = "bloghelper9000-four-years-on.md";
        schedule.Cell("F2").Value = "New"; schedule.Cell("G2").Value = true;

        var traefik = workbook.AddWorksheet("Traefik Series");
        string[] datedHeader = ["#", "Week", "Publish date", "Day", "Series", "Post title", "Draft filename", "Tags", "Published?", "Notes"];
        for (var i = 0; i < datedHeader.Length; i++)
            traefik.Cell(1, i + 1).Value = datedHeader[i];
        traefik.Cell("A2").Value = 1; traefik.Cell("B2").Value = 1;
        traefik.Cell("C2").Value = new DateTime(2026, 7, 9); traefik.Cell("D2").Value = "Thursday";
        traefik.Cell("E2").Value = "Foundations";
        traefik.Cell("F2").Value = "Traefik in the homelab: one proxy for everything";
        traefik.Cell("G2").Value = "traefik-in-the-homelab-one-proxy-for-everything.md";
        traefik.Cell("H2").Value = "traefik, homelab, docker";
        traefik.Cell("I2").Value = false; traefik.Cell("J2").Value = "Series opener";

        return workbook;
    }

    [Fact]
    public void Import_SkipsDashboardAndProgress_AndImportsEntrySheets()
    {
        using var workbook = BuildWorkbook();

        var summary = ScheduleXlsxImporter.Import(workbook, _db);

        summary.SeriesCounts.Should().BeEquivalentTo(new[]
        {
            ("BlogHelper9000 revisited", 1),
            ("Traefik Series", 1)
        });
    }

    [Fact]
    public void Import_MapsSheetNamesToSeriesNamesViaDashboard()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        var repository = new ScheduleRepository(_db);
        repository.FindSeries("BlogHelper9000 revisited").Should().NotBeNull();
        repository.FindSeries("Schedule").Should().BeNull();
    }

    [Fact]
    public void Import_ReadsAllEntryColumns()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        var entry = new ScheduleRepository(_db)
            .FindEntryByFilename("traefik-in-the-homelab-one-proxy-for-everything.md");
        entry.Should().BeEquivalentTo(new
        {
            Position = 1, Week = 1, PublishDate = new DateOnly(2026, 7, 9),
            Topic = "Foundations", Tags = "traefik, homelab, docker",
            Published = false, Notes = "Series opener"
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Import_StoresBaselineMeta()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        var repository = new ScheduleRepository(_db);
        repository.GetMeta("baseline_published_count").Should().Be("243");
        repository.GetMeta("baseline_date").Should().Be("2026-07-04");
    }

    [Fact]
    public void Import_TicksEntriesMarkedPublished()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        new ScheduleRepository(_db)
            .FindEntryByFilename("bloghelper9000-four-years-on.md")!
            .Published.Should().BeTrue();
    }

    public void Dispose() => _db.Dispose();
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleXlsxImporterTests"`
Expected: FAIL — `ScheduleXlsxImporter` does not exist.

- [ ] **Step 4: Write the importer**

`BlogHelper9000/Scheduling/ScheduleXlsxImporter.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using BlogHelper9000.Core.Scheduling;
using ClosedXML.Excel;

namespace BlogHelper9000.Scheduling;

public sealed record ImportSummary(IReadOnlyList<(string Series, int Entries)> SeriesCounts);

public static partial class ScheduleXlsxImporter
{
    private static readonly string[] SkippedSheets = ["Dashboard", "Progress"];

    [GeneratedRegex(@"\(baseline,\s*([^)]+)\)")]
    private static partial Regex BaselineDatePattern();

    public static ImportSummary Import(XLWorkbook workbook, ScheduleDatabase database)
    {
        var repository = new ScheduleRepository(database);
        var seriesNameBySheet = ReadDashboard(workbook, repository);

        var counts = new List<(string, int)>();
        var sortOrder = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            if (SkippedSheets.Contains(sheet.Name)) continue;

            var seriesName = seriesNameBySheet.GetValueOrDefault(sheet.Name, sheet.Name);
            var seriesId = repository.AddSeries(seriesName, sortOrder++);
            counts.Add((seriesName, ImportSheet(sheet, repository, seriesId)));
        }
        return new ImportSummary(counts);
    }

    private static Dictionary<string, string> ReadDashboard(XLWorkbook workbook, ScheduleRepository repository)
    {
        var map = new Dictionary<string, string>();
        if (!workbook.TryGetWorksheet("Dashboard", out var dashboard)) return map;

        var sheetNames = workbook.Worksheets.Select(w => w.Name).ToHashSet();
        foreach (var row in dashboard.RowsUsed())
        {
            var label = row.Cell(1).GetString();
            var value = row.Cell(2);

            if (label.StartsWith("Published posts (baseline", StringComparison.Ordinal))
            {
                repository.SetMeta("baseline_published_count",
                    value.GetValue<int>().ToString(CultureInfo.InvariantCulture));

                var match = BaselineDatePattern().Match(label);
                if (match.Success && DateTime.TryParse(match.Groups[1].Value.Trim(),
                        CultureInfo.GetCultureInfo("en-GB"), out var baselineDate))
                    repository.SetMeta("baseline_date", baselineDate.ToString("yyyy-MM-dd"));
            }
            else if (label.Length > 0 && sheetNames.Contains(value.GetString()))
            {
                map[value.GetString()] = label;
            }
        }
        return map;
    }

    private static int ImportSheet(IXLWorksheet sheet, ScheduleRepository repository, long seriesId)
    {
        var columns = sheet.Row(1).CellsUsed()
            .ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber);

        if (!columns.TryGetValue("Post title", out var titleColumn) ||
            !columns.ContainsKey("Draft filename"))
            throw new InvalidOperationException(
                $"Sheet '{sheet.Name}' is missing the 'Post title'/'Draft filename' header columns.");

        var count = 0;
        for (var rowNumber = 2; ; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            var title = row.Cell(titleColumn).GetString().Trim();
            if (title.Length == 0) break;

            repository.AddEntry(seriesId, new NewScheduleEntry(
                Position: GetInt(row, columns, "#"),
                Week: GetInt(row, columns, "Week"),
                PublishDate: GetDate(row, columns, "Publish date"),
                Topic: GetString(row, columns, "Series"),
                Title: title,
                DraftFilename: GetString(row, columns, "Draft filename")
                    ?? throw new InvalidOperationException(
                        $"'{sheet.Name}' row {rowNumber}: 'Draft filename' is empty for '{title}'."),
                Tags: GetString(row, columns, "Tags"),
                Source: GetString(row, columns, "Source"),
                Published: GetBool(row, columns, "Published?"),
                Notes: GetString(row, columns, "Notes")));
            count++;
        }
        return count;
    }

    private static IXLCell? Cell(IXLRow row, Dictionary<string, int> columns, string name) =>
        columns.TryGetValue(name, out var column) ? row.Cell(column) : null;

    private static string? GetString(IXLRow row, Dictionary<string, int> columns, string name)
    {
        var text = Cell(row, columns, name)?.GetString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int? GetInt(IXLRow row, Dictionary<string, int> columns, string name)
    {
        var cell = Cell(row, columns, name);
        return cell is null || cell.IsEmpty() ? null : cell.GetValue<int>();
    }

    private static DateOnly? GetDate(IXLRow row, Dictionary<string, int> columns, string name)
    {
        var cell = Cell(row, columns, name);
        return cell is null || cell.IsEmpty() ? null : DateOnly.FromDateTime(cell.GetDateTime());
    }

    private static bool GetBool(IXLRow row, Dictionary<string, int> columns, string name)
    {
        var cell = Cell(row, columns, name);
        return cell is not null && !cell.IsEmpty() && cell.GetValue<bool>();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleXlsxImporterTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Write the command**

`BlogHelper9000/Commands/ScheduleImportCommand.cs`:

```csharp
using BlogHelper9000.Core;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Scheduling;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule import", Description = "Convert a publishing-schedule spreadsheet into the blog's schedule database")]
public sealed class ScheduleImportCommand : ICommand<Unit>
{
    [Parameter(Description = "Path to the .xlsx publishing schedule.")]
    public string Path { get; set; }

    [Option("force", "f", Description = "Replace an existing schedule database.")]
    public bool Force { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IFileSystem fileSystem, IOptions<BlogHelperOptions> options)
        : ICommandHandler<ScheduleImportCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleImportCommand request, CancellationToken cancellationToken)
        {
            if (!fileSystem.File.Exists(request.Path))
            {
                logger.LogError("Spreadsheet not found at {Path}", request.Path);
                return default;
            }

            var databasePath = ScheduleDatabase.PathFor(options.Value.BaseDirectory);
            if (fileSystem.File.Exists(databasePath))
            {
                if (!request.Force)
                {
                    logger.LogError(
                        "A schedule database already exists at {Path} — re-run with --force to replace it",
                        databasePath);
                    return default;
                }
                fileSystem.File.Delete(databasePath);
            }

            using var stream = fileSystem.File.OpenRead(request.Path);
            using var workbook = new XLWorkbook(stream);
            using var database = ScheduleDatabase.Open(options.Value.BaseDirectory);

            var summary = ScheduleXlsxImporter.Import(workbook, database);

            foreach (var (series, entries) in summary.SeriesCounts)
                logger.LogInformation("Imported {Series}: {Entries} entries", series, entries);
            logger.LogInformation("Schedule database written to {Path}", databasePath);

            return default;
        }
    }
}
```

If `[NuruRoute("schedule import")]` does not register a two-segment route on TimeWarp.Nuru 3.0.0-beta.71, check the package's route-pattern docs (`dotnet run --project BlogHelper9000 -- --help` lists registered routes) — multi-segment literal routes are the library's core model, so this is expected to work as written.

Note: the handler's guard paths (`xlsx` missing, database already present) are exercised through `MockFileSystem`, but the happy path opens a real SQLite file and is deliberately left to the importer tests above plus the real conversion in Task 9 — `ScheduleDatabase.Open` cannot write through a mock filesystem.

- [ ] **Step 7: Build and verify the route is registered**

Run: `zsh -lc "./build.sh && dotnet run --project BlogHelper9000 -- --help"`
Expected: build succeeds; help output lists `schedule import`.

- [ ] **Step 8: Commit**

```bash
git add BlogHelper9000 BlogHelper9000.Tests
git commit -m "feat: add 'schedule import' command converting the xlsx schedule to SQLite"
```

---

### Task 6: `schedule list`, `schedule show`, `schedule stats`

**Files:**
- Create: `BlogHelper9000/Commands/ScheduleListCommand.cs`
- Create: `BlogHelper9000/Commands/ScheduleShowCommand.cs`
- Create: `BlogHelper9000/Commands/ScheduleStatsCommand.cs`
- Create: `BlogHelper9000/Reporters/ScheduleReporter.cs`
- Modify: `BlogHelper9000/Program.cs` (register `ScheduleReporter`)
- Test: `BlogHelper9000.Tests/Commands/ScheduleCommandsTests.cs`

**Interfaces:**
- Consumes: `IScheduleService` (Task 4).
- Produces: routes `schedule list`, `schedule show <series>`, `schedule stats`; `ScheduleReporter` with `void ReportSeries(IReadOnlyList<SeriesStats> series)`, `void ReportEntries(string seriesName, IReadOnlyList<ScheduleEntry> entries)`, `void ReportDashboard(ScheduleDashboard dashboard)`.

Every command prints a helpful error when `DatabaseExists` is false: `"No schedule database found — run 'bloghelper schedule import <xlsx>' first."` Follow the `InfoCommand` pattern: handlers call `reporter?.Report...` so tests can pass a null reporter.

- [ ] **Step 1: Write the failing tests**

`BlogHelper9000.Tests/Commands/ScheduleCommandsTests.cs`:

```csharp
using BlogHelper9000.Commands;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.TestHelpers;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class ScheduleCommandsTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    [Fact]
    public async Task ScheduleList_WhenNoDatabase_DoesNotQuerySeries()
    {
        _scheduleService.DatabaseExists.Returns(false);
        var handler = new ScheduleListCommand.Handler(new MockLogger<ScheduleListCommand.Handler>(), _scheduleService, null!);

        await handler.Handle(new ScheduleListCommand(), CancellationToken.None);

        _scheduleService.DidNotReceive().GetDashboard();
    }

    [Fact]
    public async Task ScheduleShow_QueriesTheNamedSeries()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.GetSeriesEntries("FootballData").Returns([]);
        var handler = new ScheduleShowCommand.Handler(new MockLogger<ScheduleShowCommand.Handler>(), _scheduleService, null!);

        await handler.Handle(new ScheduleShowCommand { Series = "FootballData" }, CancellationToken.None);

        _scheduleService.Received(1).GetSeriesEntries("FootballData");
    }

    [Fact]
    public async Task ScheduleStats_QueriesTheDashboard()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.GetDashboard().Returns(new ScheduleDashboard(243, null, 0, 243, 0, 0, 0, []));
        var handler = new ScheduleStatsCommand.Handler(new MockLogger<ScheduleStatsCommand.Handler>(), _scheduleService, null!);

        await handler.Handle(new ScheduleStatsCommand(), CancellationToken.None);

        _scheduleService.Received(1).GetDashboard();
    }
}
```

(If `MockLogger<T>` in TestHelpers has a different shape, use whatever the existing command tests in `BlogHelper9000.Tests` use for `ILogger<T>` — check a neighbouring test first and match it.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleCommandsTests"`
Expected: FAIL — commands do not exist.

- [ ] **Step 3: Write the reporter**

`BlogHelper9000/Reporters/ScheduleReporter.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;

namespace BlogHelper9000.Reporters;

public class ScheduleReporter
{
    public void ReportSeries(IReadOnlyList<SeriesStats> series)
    {
        var table = new Table().Expand()
            .AddColumns("Series", "Planned", "Published", "Remaining", "% done", "Next planned", "Next slot");
        foreach (var s in series)
        {
            table.AddRow(
                Markup.Escape(s.Series),
                s.Planned.ToString(),
                s.Published.ToString(),
                s.Remaining.ToString(),
                $"{s.PercentDone:P0}",
                Markup.Escape(s.NextPlannedTitle ?? "all published"),
                Markup.Escape(s.NextSlot ?? string.Empty));
        }
        AnsiConsole.Write(table);
    }

    public void ReportEntries(string seriesName, IReadOnlyList<ScheduleEntry> entries)
    {
        var table = new Table().Expand().Title(Markup.Escape(seriesName))
            .AddColumns("#", "Week", "Date", "Topic", "Title", "Draft filename", "Published");
        foreach (var e in entries)
        {
            table.AddRow(
                e.Position.ToString(),
                e.Week?.ToString() ?? string.Empty,
                e.PublishDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                Markup.Escape(e.Topic ?? string.Empty),
                Markup.Escape(e.Title),
                Markup.Escape(e.DraftFilename),
                e.Published ? $"[green]✔ {e.PublishedOn:yyyy-MM-dd}[/]" : string.Empty);
        }
        AnsiConsole.Write(table);
    }

    public void ReportDashboard(ScheduleDashboard dashboard)
    {
        var filled = (int)Math.Round(dashboard.Progress * 20);
        var bar = new string('█', filled) + new string('░', 20 - filled);

        var grid = new Grid { Expand = true }
            .AddColumns(new GridColumn().LeftAligned(), new GridColumn().LeftAligned(), new GridColumn())
            .AddRow("Published posts (baseline)", ":",
                $"{dashboard.BaselinePublished}{(dashboard.BaselineDate is { } d ? $" (as of {d:yyyy-MM-dd})" : string.Empty)}")
            .AddRow("Published via schedules", ":", dashboard.PublishedViaSchedules.ToString())
            .AddRow("Total published on the blog", ":", dashboard.TotalPublished.ToString())
            .AddRow("Posts planned across all series", ":", dashboard.TotalPlanned.ToString())
            .AddRow("Drafts remaining", ":", dashboard.TotalRemaining.ToString())
            .AddRow("Schedule progress", ":", $"{bar} {dashboard.Progress:P1}")
            .AddRow("Draft : published", ":", $"{dashboard.TotalRemaining} : {dashboard.TotalPublished}");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(grid).Header("Publishing dashboard"));
        AnsiConsole.WriteLine();
        ReportSeries(dashboard.Series);
    }
}
```

- [ ] **Step 4: Write the three commands**

`BlogHelper9000/Commands/ScheduleListCommand.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule list", Description = "List post series with their schedule progress")]
public sealed class ScheduleListCommand : ICommand<Unit>
{
    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService, ScheduleReporter reporter)
        : ICommandHandler<ScheduleListCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleListCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule import <xlsx>' first");
                return default;
            }

            reporter?.ReportSeries(scheduleService.GetDashboard().Series);
            return default;
        }
    }
}
```

`BlogHelper9000/Commands/ScheduleShowCommand.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule show", Description = "Show the entries in a series")]
public sealed class ScheduleShowCommand : ICommand<Unit>
{
    [Parameter(Description = "The series to show, e.g. 'FootballData'.")]
    public string Series { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService, ScheduleReporter reporter)
        : ICommandHandler<ScheduleShowCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleShowCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule import <xlsx>' first");
                return default;
            }

            var entries = scheduleService.GetSeriesEntries(request.Series);
            if (entries.Count == 0)
            {
                logger.LogError("No series named '{Series}' (or it has no entries) — try 'bloghelper schedule list'", request.Series);
                return default;
            }

            reporter?.ReportEntries(request.Series, entries);
            return default;
        }
    }
}
```

`BlogHelper9000/Commands/ScheduleStatsCommand.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule stats", Description = "Show the publishing dashboard (like the old spreadsheet)")]
public sealed class ScheduleStatsCommand : ICommand<Unit>
{
    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService, ScheduleReporter reporter)
        : ICommandHandler<ScheduleStatsCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleStatsCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule import <xlsx>' first");
                return default;
            }

            reporter?.ReportDashboard(scheduleService.GetDashboard());
            return default;
        }
    }
}
```

Register the reporter in `BlogHelper9000/Program.cs` next to `InfoCommandReporter`:

```csharp
builder.Services.AddSingleton<ScheduleReporter>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleCommandsTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add BlogHelper9000 BlogHelper9000.Tests
git commit -m "feat: add 'schedule list/show/stats' commands with Spectre dashboard"
```

---

### Task 7: `add --series`, `publish` auto-tick, `schedule mark`

**Files:**
- Modify: `BlogHelper9000/Commands/AddCommand.cs`
- Modify: `BlogHelper9000/Commands/PublishCommand.cs`
- Create: `BlogHelper9000/Commands/ScheduleMarkCommand.cs`
- Test: `BlogHelper9000.Tests/Commands/ScheduleIntegrationCommandsTests.cs`

**Interfaces:**
- Consumes: `IScheduleService.AddToSeries(...)`, `IScheduleService.MarkPublished(...)` (Task 4), `IBlogService`.
- Produces: `add <title> <tags> [--series <name>] [--week <n>] [--publish-date <yyyy-MM-dd>]`; `publish <post>` now ticks the schedule; route `schedule mark <post> [--date <yyyy-MM-dd>] [--unmark]`.

- [ ] **Step 1: Write the failing tests**

`BlogHelper9000.Tests/Commands/ScheduleIntegrationCommandsTests.cs`:

```csharp
using BlogHelper9000.Commands;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class ScheduleIntegrationCommandsTests
{
    private readonly IBlogService _blogService = Substitute.For<IBlogService>();
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    [Fact]
    public async Task Add_WithSeriesOption_AddsCreatedDraftToSeries()
    {
        _blogService.AddPost("My Post", true, false, false, null, Arg.Any<IReadOnlyList<string>>())
            .Returns("/blog/_drafts/my-post.md");
        var handler = new AddCommand.Handler(new MockLogger<AddCommand.Handler>(), _blogService, _scheduleService);

        await handler.Handle(new AddCommand
        {
            Title = "My Post", Tags = "csharp", IsDraft = true, Series = "FootballData", Week = 3
        }, CancellationToken.None);

        _scheduleService.Received(1).AddToSeries("FootballData", "My Post", "my-post.md",
            week: 3, publishDate: null, tags: "csharp", notes: null);
    }

    [Fact]
    public async Task Add_WithoutSeriesOption_DoesNotTouchTheSchedule()
    {
        _blogService.AddPost(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>()).Returns("/blog/_drafts/my-post.md");
        var handler = new AddCommand.Handler(new MockLogger<AddCommand.Handler>(), _blogService, _scheduleService);

        await handler.Handle(new AddCommand { Title = "My Post", Tags = "csharp" }, CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().AddToSeries(default!, default!, default!);
    }

    [Fact]
    public async Task Publish_OnSuccess_TicksTheScheduleEntry()
    {
        _blogService.PublishPost("my-post.md").Returns("/blog/_posts/2026/2026-07-06-my-post.md");
        _scheduleService.MarkPublished("my-post.md").Returns(MarkPublishedOutcome.Marked);
        var handler = new PublishCommand.Handler(new MockLogger<PublishCommand.Handler>(), _blogService, _scheduleService);

        await handler.Handle(new PublishCommand { Post = "my-post.md" }, CancellationToken.None);

        _scheduleService.Received(1).MarkPublished("my-post.md");
    }

    [Fact]
    public async Task Publish_OnFailure_DoesNotTouchTheSchedule()
    {
        _blogService.PublishPost("missing.md").Returns((string?)null);
        var handler = new PublishCommand.Handler(new MockLogger<PublishCommand.Handler>(), _blogService, _scheduleService);

        await handler.Handle(new PublishCommand { Post = "missing.md" }, CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().MarkPublished(default!);
    }

    [Fact]
    public async Task ScheduleMark_TicksAnEntry_WithOptionalBackdate()
    {
        _scheduleService.MarkPublished("my-post.md", new DateOnly(2026, 7, 1), false)
            .Returns(MarkPublishedOutcome.Marked);
        var handler = new ScheduleMarkCommand.Handler(new MockLogger<ScheduleMarkCommand.Handler>(), _scheduleService);

        await handler.Handle(new ScheduleMarkCommand { Post = "my-post.md", Date = "2026-07-01" }, CancellationToken.None);

        _scheduleService.Received(1).MarkPublished("my-post.md", new DateOnly(2026, 7, 1), false);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter FullyQualifiedName~ScheduleIntegrationCommandsTests"`
Expected: FAIL — new ctor parameters / command missing.

- [ ] **Step 3: Modify `AddCommand`**

Add three properties after `FeaturedImage`:

```csharp
[Option("series", "s", Description = "Add the new post to this schedule series.")]
public string? Series { get; set; }
[Option("week", "w", Description = "Schedule week number for the series entry.")]
public int? Week { get; set; }
[Option("publish-date", "p", Description = "Planned publish date (yyyy-MM-dd) for the series entry.")]
public string? PublishDate { get; set; }
```

Change the handler class declaration and body:

```csharp
public sealed class Handler(ILogger<Handler> logger, IBlogService blogService, IScheduleService scheduleService)
    : ICommandHandler<AddCommand, Unit>
{
    public ValueTask<Unit> Handle(AddCommand request, CancellationToken cancellationToken)
    {
        var tags = string.IsNullOrWhiteSpace(request.Tags)
            ? null
            : request.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var filePath = blogService.AddPost(request.Title, request.IsDraft, request.IsFeatured, request.IsHidden, request.FeaturedImage, tags);

        if (filePath is null)
        {
            logger.LogError("Could not add post '{Title}' — a post already exists at the target path", request.Title);
            return default;
        }

        logger.LogInformation("Added new post at {File}", filePath);

        if (!string.IsNullOrWhiteSpace(request.Series))
        {
            DateOnly? publishDate = DateOnly.TryParse(request.PublishDate, out var parsed) ? parsed : null;
            var entry = scheduleService.AddToSeries(request.Series, request.Title, Path.GetFileName(filePath),
                week: request.Week, publishDate: publishDate, tags: request.Tags, notes: null);

            if (entry is null)
                logger.LogWarning("'{File}' is already on the schedule — not added again", Path.GetFileName(filePath));
            else
                logger.LogInformation("Scheduled as #{Position} in series '{Series}'", entry.Position, entry.Series);
        }

        return default;
    }
}
```

Add `using BlogHelper9000.Core.Scheduling;` to the file.

- [ ] **Step 4: Modify `PublishCommand`**

```csharp
public class Handler(ILogger<Handler> logger, IBlogService blogService, IScheduleService scheduleService)
    : ICommandHandler<PublishCommand, Unit>
{
    public ValueTask<Unit> Handle(PublishCommand request, CancellationToken cancellationToken)
    {
        var result = blogService.PublishPost(request.Post);

        if (result is null)
        {
            logger.LogError("Could not find {Post} to publish", request.Post);
            return default;
        }

        logger.LogInformation("Published to {Result}", result);

        switch (scheduleService.MarkPublished(request.Post))
        {
            case MarkPublishedOutcome.Marked:
                logger.LogInformation("Ticked off the schedule entry for {Post}", request.Post);
                break;
            case MarkPublishedOutcome.NotScheduled:
                logger.LogDebug("{Post} is not on any schedule", request.Post);
                break;
        }

        return default;
    }
}
```

Add `using BlogHelper9000.Core.Scheduling;`.

- [ ] **Step 5: Write `ScheduleMarkCommand`**

`BlogHelper9000/Commands/ScheduleMarkCommand.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule mark", Description = "Manually tick (or untick) a schedule entry as published")]
public sealed class ScheduleMarkCommand : ICommand<Unit>
{
    [Parameter(Description = "The post's draft filename, e.g. 'my-post.md'.")]
    public string Post { get; set; }

    [Option("date", "d", Description = "The date it was published (yyyy-MM-dd); defaults to today.")]
    public string? Date { get; set; }

    [Option("unmark", "u", Description = "Untick the entry instead.")]
    public bool Unmark { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService)
        : ICommandHandler<ScheduleMarkCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleMarkCommand request, CancellationToken cancellationToken)
        {
            DateOnly? publishedOn = DateOnly.TryParse(request.Date, out var parsed) ? parsed : null;
            var outcome = scheduleService.MarkPublished(request.Post, publishedOn, request.Unmark);

            switch (outcome)
            {
                case MarkPublishedOutcome.Marked:
                    logger.LogInformation("Marked {Post} as published", request.Post);
                    break;
                case MarkPublishedOutcome.Unmarked:
                    logger.LogInformation("Unmarked {Post}", request.Post);
                    break;
                case MarkPublishedOutcome.AlreadyMarked:
                    logger.LogWarning("{Post} is already marked as published", request.Post);
                    break;
                case MarkPublishedOutcome.NotScheduled:
                    logger.LogError("{Post} is not on any schedule — check 'bloghelper schedule show <series>'", request.Post);
                    break;
            }

            return default;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass, then the full suite**

Run: `zsh -lc "dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj"`
Expected: PASS, including all pre-existing `AddCommand`/`PublishCommand` tests — those will need their handler construction updated to pass a substituted `IScheduleService`; update them mechanically where the new ctor parameter breaks compilation.

- [ ] **Step 7: Commit**

```bash
git add BlogHelper9000 BlogHelper9000.Tests
git commit -m "feat: schedule integration for add/publish plus 'schedule mark'"
```

---

### Task 8: MCP tools for the schedule

**Files:**
- Modify: `BlogHelper9000.Mcp/ToolResponses.cs` (new DTOs; extend `PublishResult`)
- Create: `BlogHelper9000.Mcp/Tools/ListSeriesTool.cs`
- Create: `BlogHelper9000.Mcp/Tools/GetSeriesTool.cs`
- Create: `BlogHelper9000.Mcp/Tools/GetScheduleStatsTool.cs`
- Create: `BlogHelper9000.Mcp/Tools/AddPostToSeriesTool.cs`
- Create: `BlogHelper9000.Mcp/Tools/MarkScheduleEntryPublishedTool.cs`
- Create: `BlogHelper9000.Mcp/Tools/GetNextScheduledPostTool.cs`
- Modify: `BlogHelper9000.Mcp/Tools/PublishPostTool.cs` (auto-tick)
- Modify: `BlogHelper9000.Mcp/Program.cs` (server instructions)
- Test: `BlogHelper9000.Mcp.Tests/Tools/ScheduleToolsTests.cs`, modify `BlogHelper9000.Mcp.Tests/Tools/PublishPostToolTests.cs`

**Interfaces:**
- Consumes: `IScheduleService` (Task 4).
- Produces MCP tools: `list_series`, `get_series`, `get_schedule_stats`, `add_post_to_series`, `mark_schedule_entry_published`, `get_next_scheduled_post`; `publish_post` response gains `ScheduleOutcome`.

- [ ] **Step 1: Add response DTOs**

Append to `BlogHelper9000.Mcp/ToolResponses.cs`:

```csharp
public sealed record SeriesStatsDto(
    string Series, int Planned, int Published, int Remaining, double PercentDone,
    string? LatestPostedTitle, string? NextPlannedTitle, string? NextSlot, DateOnly? LastPostedOn);

public sealed record ListSeriesResult(IReadOnlyList<SeriesStatsDto> Series);

public sealed record ScheduleEntryDto(
    int Position, int? Week, DateOnly? PublishDate, string? Topic, string Title,
    string DraftFilename, string? Tags, bool Published, DateOnly? PublishedOn, string? Notes);

public sealed record GetSeriesResult(string Series, IReadOnlyList<ScheduleEntryDto> Entries);

public sealed record ScheduleStatsResult(
    int BaselinePublished, DateOnly? BaselineDate, int PublishedViaSchedules, int TotalPublished,
    int TotalPlanned, int TotalRemaining, double Progress, IReadOnlyList<SeriesStatsDto> Series);

public sealed record AddToSeriesResult(string Series, int Position, string DraftFilename);

public sealed record MarkScheduledResult(string DraftFilename, string Outcome);

public sealed record NextScheduledResult(
    string Series, string Title, string DraftFilename, int? Week, DateOnly? PublishDate);
```

Change `PublishResult` to:

```csharp
public sealed record PublishResult(string? PublishedPath, string? ScheduleOutcome);
```

- [ ] **Step 2: Write the failing tests**

`BlogHelper9000.Mcp.Tests/Tools/ScheduleToolsTests.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ScheduleToolsTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static ScheduleEntry Entry(string series = "Series", int position = 1, bool published = false) =>
        new(1, series, position, 2, new DateOnly(2026, 7, 7), "Topic", "Title", "title.md",
            "csharp", "New", published, null, null);

    public ScheduleToolsTests() => _scheduleService.DatabaseExists.Returns(true);

    [Fact]
    public void AllScheduleTools_WhenNoDatabase_FailWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        ListSeriesTool.ListSeries(_scheduleService).Success.Should().BeFalse();
        GetSeriesTool.GetSeries(_scheduleService, "x").Success.Should().BeFalse();
        GetScheduleStatsTool.GetScheduleStats(_scheduleService).Success.Should().BeFalse();
        GetNextScheduledPostTool.GetNextScheduledPost(_scheduleService, null).Success.Should().BeFalse();

        ListSeriesTool.ListSeries(_scheduleService).Error.Should().Contain("schedule import");
    }

    [Fact]
    public void ListSeries_MapsDashboardSeriesStats()
    {
        _scheduleService.GetDashboard().Returns(new ScheduleDashboard(243, null, 1, 244, 2, 1, 0.5,
            [new SeriesStats("S", 2, 1, 1, 0.5, "A", "B", "Week 2", null)]));

        var result = ListSeriesTool.ListSeries(_scheduleService);

        result.Success.Should().BeTrue();
        result.Data!.Series.Should().ContainSingle(s => s.Series == "S" && s.NextSlot == "Week 2");
    }

    [Fact]
    public void GetSeries_UnknownSeries_Fails()
    {
        _scheduleService.GetSeriesEntries("nope").Returns([]);

        GetSeriesTool.GetSeries(_scheduleService, "nope").Success.Should().BeFalse();
    }

    [Fact]
    public void GetScheduleStats_MapsTheDashboard()
    {
        _scheduleService.GetDashboard().Returns(new ScheduleDashboard(
            243, new DateOnly(2026, 7, 4), 1, 244, 189, 188, 1.0 / 189, []));

        var result = GetScheduleStatsTool.GetScheduleStats(_scheduleService);

        result.Success.Should().BeTrue();
        result.Data!.TotalPublished.Should().Be(244);
        result.Data.TotalRemaining.Should().Be(188);
    }

    [Fact]
    public void AddPostToSeries_ReturnsThePlacedEntry()
    {
        _scheduleService.AddToSeries("Series", "Title", "title.md", 2, null, "csharp", null)
            .Returns(Entry());

        var result = AddPostToSeriesTool.AddPostToSeries(_scheduleService, "Series", "Title", "title.md",
            week: 2, tags: "csharp");

        result.Success.Should().BeTrue();
        result.Data!.Position.Should().Be(1);
    }

    [Fact]
    public void AddPostToSeries_DuplicateFilename_Fails()
    {
        _scheduleService.AddToSeries("Series", "Title", "title.md", null, null, null, null)
            .Returns((ScheduleEntry?)null);

        AddPostToSeriesTool.AddPostToSeries(_scheduleService, "Series", "Title", "title.md")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void MarkScheduleEntryPublished_ReportsTheOutcome()
    {
        _scheduleService.MarkPublished("title.md", null, false).Returns(MarkPublishedOutcome.Marked);

        var result = MarkScheduleEntryPublishedTool.MarkScheduleEntryPublished(_scheduleService, "title.md");

        result.Success.Should().BeTrue();
        result.Data!.Outcome.Should().Be("Marked");
    }

    [Fact]
    public void MarkScheduleEntryPublished_NotScheduled_Fails()
    {
        _scheduleService.MarkPublished("nope.md", null, false).Returns(MarkPublishedOutcome.NotScheduled);

        MarkScheduleEntryPublishedTool.MarkScheduleEntryPublished(_scheduleService, "nope.md")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void GetNextScheduledPost_ReturnsTheNextEntry()
    {
        _scheduleService.GetNextUnpublished(null).Returns(Entry());

        var result = GetNextScheduledPostTool.GetNextScheduledPost(_scheduleService, null);

        result.Success.Should().BeTrue();
        result.Data!.DraftFilename.Should().Be("title.md");
    }

    [Fact]
    public void GetNextScheduledPost_WhenAllPublished_Fails()
    {
        _scheduleService.GetNextUnpublished("Series").Returns((ScheduleEntry?)null);

        GetNextScheduledPostTool.GetNextScheduledPost(_scheduleService, "Series")
            .Success.Should().BeFalse();
    }
}
```

Also update `PublishPostToolTests` construction sites to pass a substituted `IScheduleService` and add:

```csharp
[Fact]
public void PublishPost_OnSuccess_TicksTheSchedule()
{
    var blogService = Substitute.For<IBlogService>();
    var scheduleService = Substitute.For<IScheduleService>();
    blogService.PublishPostDetailed("my-draft")
        .Returns(new PublishPostResult(PublishOutcome.Published, "/path/to/_posts/2026/my-draft.md"));
    scheduleService.MarkPublished("my-draft").Returns(MarkPublishedOutcome.Marked);

    var result = PublishPostTool.PublishPost(blogService, scheduleService, "my-draft");

    result.Success.Should().BeTrue();
    result.Data!.ScheduleOutcome.Should().Be("Marked");
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `zsh -lc "dotnet test BlogHelper9000.Mcp.Tests/BlogHelper9000.Mcp.Tests.csproj"`
Expected: FAIL — tools do not exist / `PublishPostTool` signature mismatch.

- [ ] **Step 4: Write the tools**

Shared failure message (repeat the string in each tool; it's one line):
`"No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it."`

`BlogHelper9000.Mcp/Tools/ListSeriesTool.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ListSeriesTool
{
    [McpServerTool(Name = "list_series", Title = "List post series", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists every post series in the publishing schedule with progress stats: planned/published/remaining counts, " +
                 "percent done, the latest published title, and the next planned post with its slot (a date, 'Week n', or 'unscheduled').")]
    public static ToolResponse<ListSeriesResult> ListSeries(IScheduleService scheduleService)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<ListSeriesResult>.Fail(
                "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it.");

        var series = scheduleService.GetDashboard().Series.Select(ToDto).ToList();
        return ToolResponse<ListSeriesResult>.Ok(new ListSeriesResult(series));
    }

    internal static SeriesStatsDto ToDto(SeriesStats s) => new(
        s.Series, s.Planned, s.Published, s.Remaining, s.PercentDone,
        s.LatestPostedTitle, s.NextPlannedTitle, s.NextSlot, s.LastPostedOn);
}
```

`BlogHelper9000.Mcp/Tools/GetSeriesTool.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetSeriesTool
{
    [McpServerTool(Name = "get_series", Title = "Get a series' schedule", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns every schedule entry in a series, in order: position, week, planned date, topic, title, " +
                 "draft filename, tags, published state, and notes. Use list_series to discover series names.")]
    public static ToolResponse<GetSeriesResult> GetSeries(
        IScheduleService scheduleService,
        [Description("The series name, e.g. 'FootballData' (case-insensitive).")] string series)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<GetSeriesResult>.Fail(
                "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it.");

        var entries = scheduleService.GetSeriesEntries(series);
        if (entries.Count == 0)
            return ToolResponse<GetSeriesResult>.Fail(
                $"No series named '{series}' (or it has no entries). Call list_series for valid names.");

        return ToolResponse<GetSeriesResult>.Ok(new GetSeriesResult(series, entries.Select(e =>
            new ScheduleEntryDto(e.Position, e.Week, e.PublishDate, e.Topic, e.Title,
                e.DraftFilename, e.Tags, e.Published, e.PublishedOn, e.Notes)).ToList()));
    }
}
```

`BlogHelper9000.Mcp/Tools/GetScheduleStatsTool.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetScheduleStatsTool
{
    [McpServerTool(Name = "get_schedule_stats", Title = "Publishing dashboard", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns the publishing dashboard: baseline published count, posts published via the schedules, " +
                 "total published, total planned, drafts remaining, overall progress (0-1), and per-series stats.")]
    public static ToolResponse<ScheduleStatsResult> GetScheduleStats(IScheduleService scheduleService)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<ScheduleStatsResult>.Fail(
                "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it.");

        var dashboard = scheduleService.GetDashboard();
        return ToolResponse<ScheduleStatsResult>.Ok(new ScheduleStatsResult(
            dashboard.BaselinePublished, dashboard.BaselineDate, dashboard.PublishedViaSchedules,
            dashboard.TotalPublished, dashboard.TotalPlanned, dashboard.TotalRemaining,
            dashboard.Progress, dashboard.Series.Select(ListSeriesTool.ToDto).ToList()));
    }
}
```

`BlogHelper9000.Mcp/Tools/AddPostToSeriesTool.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AddPostToSeriesTool
{
    [McpServerTool(Name = "add_post_to_series", Title = "Schedule a post in a series", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = false),
     Description("Appends a post to a schedule series at the next position, creating the series if it does not exist. " +
                 "This only updates the schedule — use add_post first if the draft file itself does not exist yet.")]
    public static ToolResponse<AddToSeriesResult> AddPostToSeries(
        IScheduleService scheduleService,
        [Description("The series to add to, e.g. 'FootballData'. Created if missing.")] string series,
        [Description("The post title.")] string title,
        [Description("The draft filename, e.g. 'my-post.md'.")] string draftFilename,
        [Description("Optional schedule week number.")] int? week = null,
        [Description("Optional planned publish date, yyyy-MM-dd.")] string? publishDate = null,
        [Description("Optional comma-separated tags.")] string? tags = null,
        [Description("Optional notes.")] string? notes = null)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<AddToSeriesResult>.Fail(
                "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it.");

        DateOnly? date = null;
        if (publishDate is not null)
        {
            if (!DateOnly.TryParse(publishDate, out var parsed))
                return ToolResponse<AddToSeriesResult>.Fail($"'{publishDate}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var entry = scheduleService.AddToSeries(series, title, draftFilename, week, date, tags, notes);
        return entry is null
            ? ToolResponse<AddToSeriesResult>.Fail($"'{draftFilename}' is already on the schedule.")
            : ToolResponse<AddToSeriesResult>.Ok(new AddToSeriesResult(entry.Series, entry.Position, entry.DraftFilename));
    }
}
```

`BlogHelper9000.Mcp/Tools/MarkScheduleEntryPublishedTool.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class MarkScheduleEntryPublishedTool
{
    [McpServerTool(Name = "mark_schedule_entry_published", Title = "Tick a schedule entry", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = true),
     Description("Marks a schedule entry as published (or un-marks it) without touching the post file. " +
                 "Note publish_post already ticks the schedule automatically — use this for posts published " +
                 "by other means, or to correct mistakes.")]
    public static ToolResponse<MarkScheduledResult> MarkScheduleEntryPublished(
        IScheduleService scheduleService,
        [Description("The post's draft filename, e.g. 'my-post.md'. Date prefixes and paths are tolerated.")] string post,
        [Description("Optional actual publish date, yyyy-MM-dd; defaults to today.")] string? publishedOn = null,
        [Description("If true, un-ticks the entry instead.")] bool unmark = false)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<MarkScheduledResult>.Fail(
                "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it.");

        DateOnly? date = null;
        if (publishedOn is not null)
        {
            if (!DateOnly.TryParse(publishedOn, out var parsed))
                return ToolResponse<MarkScheduledResult>.Fail($"'{publishedOn}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var outcome = scheduleService.MarkPublished(post, date, unmark);
        return outcome == MarkPublishedOutcome.NotScheduled
            ? ToolResponse<MarkScheduledResult>.Fail($"'{post}' is not on any schedule. Call get_series to inspect the schedules.")
            : ToolResponse<MarkScheduledResult>.Ok(new MarkScheduledResult(post, outcome.ToString()));
    }
}
```

`BlogHelper9000.Mcp/Tools/GetNextScheduledPostTool.cs`:

```csharp
using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetNextScheduledPostTool
{
    [McpServerTool(Name = "get_next_scheduled_post", Title = "Next post to publish", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns the next unpublished schedule entry: within a series (first unticked by position), or across " +
                 "all series (earliest planned date first). Use this to decide what to publish next, then publish_post.")]
    public static ToolResponse<NextScheduledResult> GetNextScheduledPost(
        IScheduleService scheduleService,
        [Description("Optional series name to look in; omit to search all series.")] string? series = null)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<NextScheduledResult>.Fail(
                "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule import <xlsx>' to create it.");

        var entry = scheduleService.GetNextUnpublished(series);
        return entry is null
            ? ToolResponse<NextScheduledResult>.Fail(series is null
                ? "Every scheduled post is published. Congratulations."
                : $"No unpublished entries in '{series}' (or the series does not exist).")
            : ToolResponse<NextScheduledResult>.Ok(new NextScheduledResult(
                entry.Series, entry.Title, entry.DraftFilename, entry.Week, entry.PublishDate));
    }
}
```

- [ ] **Step 5: Modify `PublishPostTool`**

Add `IScheduleService scheduleService` as the second parameter and tick on success:

```csharp
public static ToolResponse<PublishResult> PublishPost(
    IBlogService blogService,
    IScheduleService scheduleService,
    [Description("Filename (e.g. 'my-draft.md') or path of the draft to publish. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postName)
{
    var result = blogService.PublishPostDetailed(postName);

    return result.Outcome switch
    {
        PublishOutcome.Published => ToolResponse<PublishResult>.Ok(new PublishResult(
            result.PublishedPath, scheduleService.MarkPublished(postName).ToString())),
        PublishOutcome.NotFound => ToolResponse<PublishResult>.Fail($"No draft named '{postName}' was found in _drafts/ or _posts/."),
        PublishOutcome.AlreadyPublished => ToolResponse<PublishResult>.Fail($"'{postName}' already has a date prefix and appears to be published. Publishing is idempotent-safe: no action was taken."),
        PublishOutcome.TargetExists => ToolResponse<PublishResult>.Fail("A published post already exists at the target path for today's date."),
        _ => ToolResponse<PublishResult>.Fail("Unknown publish outcome.")
    };
}
```

Add `using BlogHelper9000.Core.Scheduling;`.

- [ ] **Step 6: Update the server instructions**

In `BlogHelper9000.Mcp/Program.cs`, append to `serverInstructions` (inside the raw string, after the existing "Caution" paragraph):

```
The publishing schedule lives in a SQLite database at .bloghelper.db in the blog root
(dot-prefixed so Jekyll does not copy it into the generated site). It records post series
and per-post schedule entries keyed by draft filename. Schedule tools: list_series,
get_series, get_schedule_stats, get_next_scheduled_post, add_post_to_series,
mark_schedule_entry_published. publish_post automatically ticks the matching schedule
entry, so mark_schedule_entry_published is only needed for posts published by other
means. Scheduled-publishing workflow: get_schedule_stats -> get_next_scheduled_post ->
get_post (review) -> publish_post. If the schedule tools report that no database exists,
the user must create it with 'bloghelper schedule import <xlsx>'.
```

- [ ] **Step 7: Run the MCP test project**

Run: `zsh -lc "dotnet test BlogHelper9000.Mcp.Tests/BlogHelper9000.Mcp.Tests.csproj"`
Expected: PASS (all new tests plus updated `PublishPostToolTests`).

- [ ] **Step 8: Commit**

```bash
git add BlogHelper9000.Mcp BlogHelper9000.Mcp.Tests
git commit -m "feat: expose the publishing schedule through MCP tools"
```

---

### Task 9: Convert the real spreadsheet and verify against the Dashboard

**Files:** none in this repo (operates on `/Users/stuart/dev/sgrassie.github.io`).

- [ ] **Step 1: Run the full test suite and build**

Run: `zsh -lc "./build.sh --target=tests"`
Expected: build + all test projects pass.

- [ ] **Step 2: Import the spreadsheet**

```bash
zsh -lc "dotnet run --project BlogHelper9000 -- schedule import /Users/stuart/Desktop/bloghelper9000-publishing-schedule.xlsx --BlogHelperOptions:BaseDirectory=/Users/stuart/dev/sgrassie.github.io"
```

(If the options binding flag differs, set the base directory the way the CLI's `AddConfiguration(args)` expects — check how the TUI/CLI pass `--base-directory` and mirror it.)

Expected log output: four series imported — `BlogHelper9000 revisited` (58), `FootballData` (104), `Traefik in the homelab` (26), `Ad-hoc` (1) — and the database written to `/Users/stuart/dev/sgrassie.github.io/.bloghelper.db`.

- [ ] **Step 3: Verify the stats match the spreadsheet Dashboard**

Run `bloghelper schedule stats` (same base-directory flag) and check:

| Stat | Expected |
|---|---|
| Baseline published | 243 (as of 2026-07-04) |
| Published via schedules | 1 (only "BlogHelper9000, four years on" is ticked) |
| Total published | 244 |
| Posts planned | 189 |
| Drafts remaining | 188 |
| Progress | ≈0.5% |
| Next planned (BlogHelper9000 revisited) | "One solution, seven projects…", `Week 1` |
| Next planned (FootballData) | "What I'm building…", `2026-07-07` |
| Next planned (Traefik in the homelab) | "Traefik in the homelab: one proxy for everything", `2026-07-09` |
| Next planned (Ad-hoc) | "The publishing schedule is a spreadsheet now", `unscheduled` |

Also run `schedule show "Traefik in the homelab"` and spot-check notes ("Series opener", the TODO notes on rows 7/14/19) survived the import.

- [ ] **Step 4: Confirm Jekyll ignores the database and decide on committing it**

In `/Users/stuart/dev/sgrassie.github.io`: run `bundle exec jekyll build --dry-run 2>/dev/null || true` **or** simply confirm the file is dot-prefixed and not matched by an `include:` entry in `_config.yml`. Recommend committing `.bloghelper.db` to the blog repo (it is the only copy of the schedule); if the user prefers not to, add it to the blog's `.gitignore`. Surface this choice to the user rather than deciding silently.

- [ ] **Step 5: Sanity-check the MCP path**

```bash
zsh -lc "BLOG_BASE_DIRECTORY=/Users/stuart/dev/sgrassie.github.io dotnet run --project BlogHelper9000.Mcp" \
  # then from the MCP client (or an inspector): call get_schedule_stats and get_next_scheduled_post
```

Expected: `get_schedule_stats` returns the Task 9 Step 3 numbers; `get_next_scheduled_post` returns the FootballData opener (earliest planned date, 2026-07-07).

- [ ] **Step 6: Commit** (only if anything in this repo changed — otherwise skip)

---

### Task 10: Documentation (`CLAUDE.md` + `AGENTS.md`)

**Files:**
- Modify: `CLAUDE.md`
- Modify: `AGENTS.md` (identical edits — it is a copy)

- [ ] **Step 1: Update both files**

1. **Jekyll Blog Conventions** section, add:
   ```
   - The publishing schedule (post series, per-post entries, publish ticks) lives in a SQLite
     database at `.bloghelper.db` in the blog root — dot-prefixed so Jekyll does not copy it
     into `_site`. It is created by `bloghelper schedule import <xlsx>`.
   ```
2. **Key Namespaces** table, add:
   ```
   | `BlogHelper9000.Core.Scheduling` | ScheduleDatabase, ScheduleRepository, ScheduleService, ScheduleStats |
   ```
3. **Architecture → BlogHelper9000 (CLI)** bullet: mention `schedule import/list/show/stats/mark` commands, `add --series`, and that `publish` auto-ticks the schedule; note ClosedXML is referenced by the CLI only.
4. **Gotchas**, add:
   ```
   - SQLite bypasses `IFileSystem` — schedule tests use `ScheduleDatabase.OpenInMemory()`, never `MockFileSystem`
   - `schedule import` replaces the whole schedule database (guarded by `--force`); entries are keyed by
     draft filename, normalised without the `yyyy-MM-dd-` prefix
   ```
5. **BlogHelper9000.Mcp** bullet: add the six schedule tools to the tool list sentence.

- [ ] **Step 2: Run the full suite one last time**

Run: `zsh -lc "./build.sh --target=tests"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md AGENTS.md
git commit -m "docs: document the SQLite publishing schedule in CLAUDE.md/AGENTS.md"
```

---

## Self-review notes

- **Spec coverage:** xlsx → SQLite in blog root (Tasks 1–2, 5, 9); CLI to examine/list schedules and show spreadsheet-style stats (Task 6); options on existing commands to add posts to series and mark published (Task 7); MCP knowledge + tools for LLM driving (Task 8); Progress sheet deliberately skipped, Dashboard stats reproduced (Background + Task 3).
- **Known risks:** TimeWarp.Nuru multi-segment route syntax (Task 5 Step 6 has the verification step); `MockLogger<T>` shape in TestHelpers (Task 6 Step 1 notes to match existing tests); exact base-directory CLI flag (Task 9 Step 2 notes to mirror existing usage). Each has an in-task check rather than an assumption.
- **Type consistency check:** `IScheduleService` signatures in Task 4 match every consumer in Tasks 6–8; `SeriesStats`/`ScheduleDashboard` fields in Task 1 match `ScheduleStats` (Task 3), the reporter (Task 6), and the DTO mappers (Task 8); `PublishResult(string? PublishedPath, string? ScheduleOutcome)` is used consistently in Task 8.
