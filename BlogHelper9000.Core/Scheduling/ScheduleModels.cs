namespace BlogHelper9000.Core.Scheduling;

public sealed record SeriesInfo(long Id, string Name, int SortOrder,
    DayOfWeek? CadenceDay = null, DateOnly? CadenceStart = null);

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
    DateOnly? LastPostedOn,
    int OverdueCount);

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

public sealed record UpdateEntryResult(UpdateEntryOutcome Outcome, ScheduleEntry? Entry);

public enum UpdateEntryOutcome
{
    Updated,
    NotScheduled,
    SeriesNotFound,
    InvalidPosition
}

public enum RemoveEntryOutcome
{
    Removed,
    NotScheduled
}

public enum RenameSeriesOutcome
{
    Renamed,
    NotFound,
    NameTaken
}

public enum DeleteSeriesOutcome
{
    Deleted,
    NotFound,
    NotEmpty
}

public enum SetCadenceOutcome
{
    Set,
    SeriesNotFound,
    DayMismatch
}

public sealed record DuePost(ScheduleEntry Entry, DateOnly DueDate, bool Overdue, int DaysOverdue);
