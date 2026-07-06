using BlogHelper9000.Core.Scheduling;

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
