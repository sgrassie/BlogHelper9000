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

    /// <summary>
    /// Finds the schedule entry whose draft filename matches <paramref name="post"/> (same
    /// normalisation as <see cref="MarkPublished"/>). Returns null when there is no schedule
    /// database, or the post isn't on it.
    /// </summary>
    ScheduleEntry? FindEntry(string post);
}
