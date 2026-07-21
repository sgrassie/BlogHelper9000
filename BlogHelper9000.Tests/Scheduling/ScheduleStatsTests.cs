using BlogHelper9000.Core.Scheduling;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleStatsTests
{
    private static readonly DateOnly Today = new(2026, 7, 21);
    private static readonly SeriesInfo Series = new(1, "Series", 0);

    private static ScheduleEntry Entry(int position, string title, bool published,
        int? week = null, DateOnly? date = null, DateOnly? publishedOn = null) =>
        new(position, "Series", position, week, date, null, title, $"{position}.md",
            null, null, published, publishedOn, null);

    private static SeriesInfo SeriesNamed(string name) => Series with { Name = name };

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

        var stats = ScheduleStats.ForSeries(Series, entries, Today);

        stats.Should().BeEquivalentTo(new
        {
            Series = "Series",
            Planned = 4,
            Published = 2,
            Remaining = 2,
            PercentDone = 0.5,
            LatestPostedTitle = "B",
            NextPlannedTitle = "C"
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void ForSeries_EmptySeries_IsAllZeroesWithoutDividingByZero()
    {
        var stats = ScheduleStats.ForSeries(Series, [], Today);

        stats.PercentDone.Should().Be(0);
        stats.NextPlannedTitle.Should().BeNull();
        stats.NextSlot.Should().BeNull();
        stats.OverdueCount.Should().Be(0);
    }

    [Fact]
    public void NextSlot_PrefersDate_ThenWeek_ThenUnscheduled()
    {
        ScheduleStats.ForSeries(SeriesNamed("S"), [Entry(1, "A", false, week: 3, date: new DateOnly(2026, 7, 7))], Today)
            .NextSlot.Should().Be("2026-07-07");
        ScheduleStats.ForSeries(SeriesNamed("S"), [Entry(1, "A", false, week: 3)], Today)
            .NextSlot.Should().Be("Week 3");
        ScheduleStats.ForSeries(SeriesNamed("S"), [Entry(1, "A", false)], Today)
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

        ScheduleStats.ForSeries(SeriesNamed("S"), entries, Today).LastPostedOn.Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public void OverdueCount_CountsUnpublishedEntriesPastTheirResolvedDueDate()
    {
        var seriesWithCadence = new SeriesInfo(1, "S", 0, DayOfWeek.Monday, new DateOnly(2026, 7, 6));
        var entries = new[]
        {
            // Explicit date, overdue.
            Entry(1, "Overdue-dated", published: false, date: new DateOnly(2026, 7, 14)),
            // Explicit date, due today: not overdue.
            Entry(2, "DueToday", published: false, date: Today),
            // Explicit date, in the future: not overdue.
            Entry(3, "Future", published: false, date: new DateOnly(2026, 7, 28)),
            // Week 3 -> cadence start + 14 days = 2026-07-20: overdue.
            Entry(4, "OverdueWeek", published: false, week: 3),
            // Would be overdue by date, but already published: excluded.
            Entry(5, "PublishedOverdue", published: true, date: new DateOnly(2026, 7, 1)),
        };

        ScheduleStats.ForSeries(seriesWithCadence, entries, Today).OverdueCount.Should().Be(2);
    }
}
