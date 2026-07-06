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

    internal static string NormaliseFilename(string post)
    {
        var name = Path.GetFileName(post.Trim());
        if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            name += ".md";
        return DatePrefixPattern().Replace(name, string.Empty);
    }

    public void Dispose() => _database?.Dispose();
}
