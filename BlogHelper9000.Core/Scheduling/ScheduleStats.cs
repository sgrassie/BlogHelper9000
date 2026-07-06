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
