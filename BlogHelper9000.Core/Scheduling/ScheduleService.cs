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
        var repository = Repository;
        var series = repository.FindSeries(seriesName);
        return series is null ? [] : repository.GetEntries(series.Id);
    }

    public ScheduleDashboard GetDashboard()
    {
        var repository = Repository;
        var today = Today;
        var perSeries = repository.ListSeries()
            .Select(s => ScheduleStats.ForSeries(s, repository.GetEntries(s.Id), today))
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

        repository.AddEntry(seriesId, new NewScheduleEntry(
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

    public ScheduleEntry? FindEntry(string post)
    {
        if (!DatabaseExists) return null;

        return Repository.FindEntryByFilename(NormaliseFilename(post));
    }

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date);

    public UpdateEntryResult UpdateEntry(string post, string? series = null, int? position = null,
        int? week = null, DateOnly? publishDate = null, string? title = null,
        string? tags = null, string? notes = null,
        bool clearWeek = false, bool clearPublishDate = false)
    {
        var repository = Repository;
        var filename = NormaliseFilename(post);
        var entry = repository.FindEntryByFilename(filename);
        if (entry is null)
            return new UpdateEntryResult(UpdateEntryOutcome.NotScheduled, null);

        var currentSeries = repository.FindSeries(entry.Series)
            ?? throw new InvalidOperationException($"Schedule entry '{filename}' references unknown series '{entry.Series}'.");

        var targetSeries = currentSeries;
        var moving = false;
        if (series is not null)
        {
            var found = repository.FindSeries(series);
            if (found is null)
                return new UpdateEntryResult(UpdateEntryOutcome.SeriesNotFound, null);
            moving = found.Id != currentSeries.Id;
            targetSeries = found;
        }

        var newWeek = clearWeek ? null : week ?? entry.Week;
        var newPublishDate = clearPublishDate ? null : publishDate ?? entry.PublishDate;
        var newTitle = title ?? entry.Title;
        var newTags = tags ?? entry.Tags;
        var newNotes = notes ?? entry.Notes;

        if (!moving && position is null)
        {
            // Scalar-only change (or same-series/no-position-change): a direct update is enough.
            repository.UpdateEntry(entry.Id, targetSeries.Id, entry.Position, newWeek,
                newPublishDate, newTitle, newTags, newNotes);
            return new UpdateEntryResult(UpdateEntryOutcome.Updated,
                repository.FindEntryByFilename(filename));
        }

        // Series and/or position change: recompute the target series' order and renumber.
        // (When staying, this entry is filtered out of its own series' current list; when
        // moving, it isn't present in the target series yet, so the filter is a no-op.)
        var targetEntries = repository.GetEntries(targetSeries.Id).Where(e => e.Id != entry.Id).ToList();

        var count = targetEntries.Count + 1;
        var requestedPosition = position ?? (moving ? count : entry.Position);
        if (requestedPosition < 1 || requestedPosition > count)
            return new UpdateEntryResult(UpdateEntryOutcome.InvalidPosition, null);

        var insertIndex = requestedPosition - 1;
        targetEntries.Insert(insertIndex, entry);

        var order = targetEntries
            .Select((e, i) => (EntryId: e.Id, Position: i + 1))
            .ToList();

        // Move the entry to the target series (and set its scalar fields) at a temporary,
        // guaranteed-unoccupied position first — the requested slot may already be taken by
        // another entry, which would trip the UNIQUE(series_id, position) constraint. The
        // sentinel must sit outside Renumber's own negate-pass range (-1..-N), which is why
        // int.MinValue rather than e.g. -1 is used. Renumber then settles the whole target
        // series (and, when moving, the source series) in one conflict-free pass via its
        // negate-then-apply strategy.
        repository.UpdateEntry(entry.Id, targetSeries.Id, int.MinValue, newWeek,
            newPublishDate, newTitle, newTags, newNotes);
        repository.Renumber(targetSeries.Id, order);

        if (moving)
        {
            var sourceEntries = repository.GetEntries(currentSeries.Id);
            var sourceOrder = sourceEntries
                .Select((e, i) => (EntryId: e.Id, Position: i + 1))
                .ToList();
            repository.Renumber(currentSeries.Id, sourceOrder);
        }

        return new UpdateEntryResult(UpdateEntryOutcome.Updated,
            repository.FindEntryByFilename(filename));
    }

    public RemoveEntryOutcome RemoveEntry(string post)
    {
        var repository = Repository;
        var filename = NormaliseFilename(post);
        var entry = repository.FindEntryByFilename(filename);
        if (entry is null) return RemoveEntryOutcome.NotScheduled;

        var series = repository.FindSeries(entry.Series);
        repository.DeleteEntry(entry.Id);

        if (series is not null)
        {
            var remaining = repository.GetEntries(series.Id);
            var order = remaining
                .Select((e, i) => (EntryId: e.Id, Position: i + 1))
                .ToList();
            repository.Renumber(series.Id, order);
        }

        return RemoveEntryOutcome.Removed;
    }

    public RenameSeriesOutcome RenameSeries(string oldName, string newName)
    {
        var repository = Repository;
        var series = repository.FindSeries(oldName);
        if (series is null) return RenameSeriesOutcome.NotFound;

        var existing = repository.FindSeries(newName);
        if (existing is not null && existing.Id != series.Id)
            return RenameSeriesOutcome.NameTaken;

        repository.RenameSeries(series.Id, newName);
        return RenameSeriesOutcome.Renamed;
    }

    public DeleteSeriesOutcome DeleteSeries(string name)
    {
        var repository = Repository;
        var series = repository.FindSeries(name);
        if (series is null) return DeleteSeriesOutcome.NotFound;

        if (repository.CountEntries(series.Id) > 0)
            return DeleteSeriesOutcome.NotEmpty;

        repository.DeleteSeries(series.Id);
        return DeleteSeriesOutcome.Deleted;
    }

    public SetCadenceOutcome SetSeriesCadence(string series, DayOfWeek day, DateOnly startDate)
    {
        var repository = Repository;
        var found = repository.FindSeries(series);
        if (found is null) return SetCadenceOutcome.SeriesNotFound;

        if (startDate.DayOfWeek != day) return SetCadenceOutcome.DayMismatch;

        repository.SetSeriesCadence(found.Id, (int)day, startDate.ToString("yyyy-MM-dd"));
        return SetCadenceOutcome.Set;
    }

    public IReadOnlyList<DuePost> GetDuePosts(DateOnly? asOf = null)
    {
        var due = asOf ?? Today;
        var repository = Repository;

        var results = new List<DuePost>();
        foreach (var series in repository.ListSeries())
        {
            foreach (var entry in repository.GetEntries(series.Id))
            {
                if (entry.Published) continue;

                var dueDate = ResolveDueDate(series, entry);
                if (dueDate is null || dueDate.Value > due) continue;

                var overdue = dueDate.Value < due;
                var daysOverdue = overdue ? due.DayNumber - dueDate.Value.DayNumber : 0;
                results.Add(new DuePost(entry, dueDate.Value, overdue, daysOverdue));
            }
        }

        return results
            .OrderBy(d => d.DueDate)
            .ThenBy(d => d.Entry.Series, StringComparer.Ordinal)
            .ThenBy(d => d.Entry.Position)
            .ToList();
    }

    /// <summary>
    /// Resolves the effective due date for an entry: its explicit <see cref="ScheduleEntry.PublishDate"/>
    /// if set, else the series cadence projected onto <see cref="ScheduleEntry.Week"/> when both are
    /// available, else null (no due date can be determined).
    /// </summary>
    internal static DateOnly? ResolveDueDate(SeriesInfo series, ScheduleEntry entry)
    {
        if (entry.PublishDate is not null) return entry.PublishDate;

        if (series.CadenceDay is not null && series.CadenceStart is not null && entry.Week is not null)
            return series.CadenceStart.Value.AddDays(7 * (entry.Week.Value - 1));

        return null;
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
