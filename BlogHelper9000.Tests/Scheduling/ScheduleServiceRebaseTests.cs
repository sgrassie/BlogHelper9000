using BlogHelper9000.Core.Scheduling;
using Microsoft.Extensions.Time.Testing;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleServiceRebaseTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();
    // 2026-07-21 is a Tuesday.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly ScheduleService _service;
    private readonly ScheduleRepository _repository;

    public ScheduleServiceRebaseTests()
    {
        _service = new ScheduleService(_db, _time);
        _repository = new ScheduleRepository(_db);
    }

    private void AddEntry(long seriesId, string title, string filename, int? position = null,
        int? week = null, DateOnly? publishDate = null, bool published = false) =>
        _repository.AddEntry(seriesId,
            new NewScheduleEntry(position, week, publishDate, null, title, filename, null, "New", published, null));

    private IEnumerable<(string DraftFilename, DateOnly? PublishDate)> Dates(string series) =>
        _service.GetSeriesEntries(series).Select(e => (e.DraftFilename, e.PublishDate));

    // ---- ShiftSeries ----

    [Fact]
    public void ShiftSeries_ShiftsAllUnpublishedDatedEntriesPreservingSpacing()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 28));
        AddEntry(seriesId, "B", "b.md", publishDate: new DateOnly(2026, 8, 4));
        AddEntry(seriesId, "C", "c.md", publishDate: new DateOnly(2026, 8, 18));

        var result = _service.ShiftSeries("Alpha", 14);

        result.Outcome.Should().Be(RebaseSeriesOutcome.Rebased);
        result.EntriesMoved.Should().Be(3);
        result.OldStartDate.Should().Be(new DateOnly(2026, 7, 28));
        result.NewStartDate.Should().Be(new DateOnly(2026, 8, 11));
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 8, 11)),
            ("b.md", new DateOnly(2026, 8, 18)),
            ("c.md", new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void ShiftSeries_DeltaInWholeWeeks_LandsDatesOnTheSameWeekday()
    {
        var seriesId = _repository.AddSeries("Alpha");
        // Both Tuesdays.
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 6, 30));
        AddEntry(seriesId, "B", "b.md", publishDate: new DateOnly(2026, 7, 7));

        _service.ShiftSeries("Alpha", 21);

        Dates("Alpha").Select(d => d.PublishDate!.Value.DayOfWeek)
            .Should().OnlyContain(day => day == DayOfWeek.Tuesday);
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 7, 21)),
            ("b.md", new DateOnly(2026, 7, 28)));
    }

    [Fact]
    public void ShiftSeries_NegativeDays_PullsDatesForward()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 8, 4));

        var result = _service.ShiftSeries("Alpha", -7);

        result.Outcome.Should().Be(RebaseSeriesOutcome.Rebased);
        result.NewStartDate.Should().Be(new DateOnly(2026, 7, 28));
        Dates("Alpha").Should().Equal(("a.md", new DateOnly(2026, 7, 28)));
    }

    [Fact]
    public void ShiftSeries_PublishedEntriesAreNeverTouched()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 7), published: true);
        AddEntry(seriesId, "B", "b.md", publishDate: new DateOnly(2026, 7, 28));

        var result = _service.ShiftSeries("Alpha", 14);

        result.EntriesMoved.Should().Be(1);
        result.OldStartDate.Should().Be(new DateOnly(2026, 7, 28));
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 7, 7)),
            ("b.md", new DateOnly(2026, 8, 11)));
    }

    [Fact]
    public void ShiftSeries_EntriesWithNullPublishDate_AreLeftAloneAndNotCounted()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 28));
        AddEntry(seriesId, "B", "b.md", week: 5);

        var result = _service.ShiftSeries("Alpha", 7);

        result.EntriesMoved.Should().Be(1);
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 8, 4)),
            ("b.md", null));
    }

    [Fact]
    public void ShiftSeries_OtherSeriesAreUntouched()
    {
        var alphaId = _repository.AddSeries("Alpha");
        AddEntry(alphaId, "A", "a.md", publishDate: new DateOnly(2026, 7, 28));
        var betaId = _repository.AddSeries("Beta");
        AddEntry(betaId, "X", "x.md", publishDate: new DateOnly(2026, 7, 30));

        _service.ShiftSeries("Alpha", 14);

        Dates("Beta").Should().Equal(("x.md", new DateOnly(2026, 7, 30)));
    }

    [Fact]
    public void ShiftSeries_UnknownSeries_ReturnsSeriesNotFound()
    {
        _service.ShiftSeries("Missing", 7).Outcome.Should().Be(RebaseSeriesOutcome.SeriesNotFound);
    }

    // ---- RebaseSeries ----

    [Fact]
    public void RebaseSeries_ExplicitNewStart_ShiftsEveryUnpublishedEntryByTheSameDelta()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 28));
        AddEntry(seriesId, "B", "b.md", publishDate: new DateOnly(2026, 8, 4));

        var result = _service.RebaseSeries("Alpha", new DateOnly(2026, 8, 18));

        result.Outcome.Should().Be(RebaseSeriesOutcome.Rebased);
        result.EntriesMoved.Should().Be(2);
        result.OldStartDate.Should().Be(new DateOnly(2026, 7, 28));
        result.NewStartDate.Should().Be(new DateOnly(2026, 8, 18));
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 8, 18)),
            ("b.md", new DateOnly(2026, 8, 25)));
    }

    [Fact]
    public void RebaseSeries_StartDateIsTheEarliestUnpublishedDate_NotTheFirstPosition()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", position: 1, publishDate: new DateOnly(2026, 7, 7), published: true);
        AddEntry(seriesId, "B", "b.md", position: 2, publishDate: new DateOnly(2026, 7, 28));
        AddEntry(seriesId, "C", "c.md", position: 3, publishDate: new DateOnly(2026, 8, 4));

        var result = _service.RebaseSeries("Alpha", new DateOnly(2026, 8, 4));

        result.OldStartDate.Should().Be(new DateOnly(2026, 7, 28));
        result.NewStartDate.Should().Be(new DateOnly(2026, 8, 4));
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 7, 7)),
            ("b.md", new DateOnly(2026, 8, 4)),
            ("c.md", new DateOnly(2026, 8, 11)));
    }

    [Fact]
    public void RebaseSeries_DefaultNewStart_IsNextOccurrenceOfTheSeriesWeekdayStrictlyAfterToday()
    {
        var seriesId = _repository.AddSeries("Alpha");
        // Tuesdays, slipped into the past; today is Tuesday 2026-07-21, so the next
        // Tuesday strictly after today is 2026-07-28 (never today itself).
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 6, 30));
        AddEntry(seriesId, "B", "b.md", publishDate: new DateOnly(2026, 7, 7));

        var result = _service.RebaseSeries("Alpha");

        result.Outcome.Should().Be(RebaseSeriesOutcome.Rebased);
        result.NewStartDate.Should().Be(new DateOnly(2026, 7, 28));
        Dates("Alpha").Should().Equal(
            ("a.md", new DateOnly(2026, 7, 28)),
            ("b.md", new DateOnly(2026, 8, 4)));
    }

    [Fact]
    public void RebaseSeries_DefaultNewStart_WeekdayLaterThisWeek_LandsThisWeek()
    {
        var seriesId = _repository.AddSeries("Alpha");
        // Thursdays; today is Tuesday 2026-07-21, so the next Thursday is 2026-07-23.
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 9));

        var result = _service.RebaseSeries("Alpha");

        result.NewStartDate.Should().Be(new DateOnly(2026, 7, 23));
        Dates("Alpha").Should().Equal(("a.md", new DateOnly(2026, 7, 23)));
    }

    [Fact]
    public void RebaseSeries_UnknownSeries_ReturnsSeriesNotFound()
    {
        _service.RebaseSeries("Missing", new DateOnly(2026, 8, 4))
            .Outcome.Should().Be(RebaseSeriesOutcome.SeriesNotFound);
    }

    [Fact]
    public void RebaseSeries_AllEntriesPublished_ReturnsNothingToMove()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 7), published: true);

        var result = _service.RebaseSeries("Alpha", new DateOnly(2026, 8, 4));

        result.Outcome.Should().Be(RebaseSeriesOutcome.NothingToMove);
        Dates("Alpha").Should().Equal(("a.md", new DateOnly(2026, 7, 7)));
    }

    [Fact]
    public void RebaseSeries_OnlyUndatedUnpublishedEntries_ReturnsNothingToMove()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", week: 3);

        _service.RebaseSeries("Alpha", new DateOnly(2026, 8, 4))
            .Outcome.Should().Be(RebaseSeriesOutcome.NothingToMove);
    }

    public void Dispose() => _db.Dispose();
}
