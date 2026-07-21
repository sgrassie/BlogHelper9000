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

    /// <summary>
    /// Overlays the supplied non-null fields onto the schedule entry matching <paramref name="post"/>.
    /// <paramref name="series"/> names the TARGET series (must already exist) to move the entry into;
    /// <paramref name="position"/> is 1-based within that target series. <paramref name="clearWeek"/>
    /// and <paramref name="clearPublishDate"/> explicitly null those columns even though the
    /// corresponding value parameter is otherwise "leave unchanged when null".
    /// </summary>
    UpdateEntryResult UpdateEntry(string post, string? series = null, int? position = null,
        int? week = null, DateOnly? publishDate = null, string? title = null,
        string? tags = null, string? notes = null,
        bool clearWeek = false, bool clearPublishDate = false);

    /// <summary>Removes the schedule entry matching <paramref name="post"/> and densely renumbers
    /// the remaining entries of its series.</summary>
    RemoveEntryOutcome RemoveEntry(string post);

    /// <summary>Renames a series. A pure casing change of the same series is allowed.</summary>
    RenameSeriesOutcome RenameSeries(string oldName, string newName);

    /// <summary>Deletes a series, but only when it has no entries — never cascade-deletes.</summary>
    DeleteSeriesOutcome DeleteSeries(string name);

    /// <summary>Sets the weekly cadence (day of week + the date of week 1) used to project
    /// week-numbered entries onto calendar dates.</summary>
    SetCadenceOutcome SetSeriesCadence(string series, DayOfWeek day, DateOnly startDate);

    /// <summary>
    /// Unpublished entries whose resolved due date (explicit publish date, or cadence
    /// projected from week number) falls on or before <paramref name="asOf"/> (defaults to
    /// today), ordered by due date then series then position.
    /// </summary>
    IReadOnlyList<DuePost> GetDuePosts(DateOnly? asOf = null);
}
