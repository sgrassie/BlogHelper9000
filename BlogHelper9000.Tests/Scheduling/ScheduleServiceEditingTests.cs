using BlogHelper9000.Core.Scheduling;
using Microsoft.Extensions.Time.Testing;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleServiceEditingTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly ScheduleService _service;
    private readonly ScheduleRepository _repository;

    public ScheduleServiceEditingTests()
    {
        _service = new ScheduleService(_db, _time);
        _repository = new ScheduleRepository(_db);
    }

    private void AddEntry(long seriesId, string title, string filename, int? position = null,
        int? week = null, DateOnly? publishDate = null, bool published = false) =>
        _repository.AddEntry(seriesId,
            new NewScheduleEntry(position, week, publishDate, null, title, filename, null, "New", published, null));

    // ---- UpdateEntry ----

    [Fact]
    public void UpdateEntry_ScalarOnly_UpdatesFieldsWithoutMoving()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");
        AddEntry(seriesId, "B", "b.md");

        var result = _service.UpdateEntry("a.md", title: "A revised", tags: "csharp", notes: "updated");

        result.Outcome.Should().Be(UpdateEntryOutcome.Updated);
        result.Entry.Should().NotBeNull();
        result.Entry!.Title.Should().Be("A revised");
        result.Entry.Tags.Should().Be("csharp");
        result.Entry.Notes.Should().Be("updated");
        result.Entry.Series.Should().Be("Alpha");
        result.Entry.Position.Should().Be(1);
    }

    [Fact]
    public void UpdateEntry_ClearWeekAndPublishDate_NullsThoseColumns()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", week: 2, publishDate: new DateOnly(2026, 7, 20));

        var result = _service.UpdateEntry("a.md", clearWeek: true, clearPublishDate: true);

        result.Outcome.Should().Be(UpdateEntryOutcome.Updated);
        result.Entry!.Week.Should().BeNull();
        result.Entry.PublishDate.Should().BeNull();
    }

    [Fact]
    public void UpdateEntry_MoveEarlierWithinSeries_RenumbersDensely()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");
        AddEntry(seriesId, "B", "b.md");
        AddEntry(seriesId, "C", "c.md");

        var result = _service.UpdateEntry("c.md", position: 1);

        result.Outcome.Should().Be(UpdateEntryOutcome.Updated);
        _service.GetSeriesEntries("Alpha").Select(e => (e.DraftFilename, e.Position)).Should().Equal(
            ("c.md", 1), ("a.md", 2), ("b.md", 3));
    }

    [Fact]
    public void UpdateEntry_MoveToAnotherSeries_AppendsAtEnd_AndRenumbersSourceDensely()
    {
        var alphaId = _repository.AddSeries("Alpha");
        AddEntry(alphaId, "A", "a.md");
        AddEntry(alphaId, "B", "b.md");
        var betaId = _repository.AddSeries("Beta");
        AddEntry(betaId, "X", "x.md");

        var result = _service.UpdateEntry("a.md", series: "Beta");

        result.Outcome.Should().Be(UpdateEntryOutcome.Updated);
        result.Entry!.Series.Should().Be("Beta");
        result.Entry.Position.Should().Be(2);

        _service.GetSeriesEntries("Alpha").Select(e => (e.DraftFilename, e.Position))
            .Should().Equal(("b.md", 1));
        _service.GetSeriesEntries("Beta").Select(e => (e.DraftFilename, e.Position))
            .Should().Equal(("x.md", 1), ("a.md", 2));
    }

    [Fact]
    public void UpdateEntry_UnknownTargetSeries_ReturnsSeriesNotFound()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");

        _service.UpdateEntry("a.md", series: "Nonexistent").Outcome.Should().Be(UpdateEntryOutcome.SeriesNotFound);
    }

    [Fact]
    public void UpdateEntry_PositionOutOfRange_ReturnsInvalidPosition()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");
        AddEntry(seriesId, "B", "b.md");

        _service.UpdateEntry("a.md", position: 0).Outcome.Should().Be(UpdateEntryOutcome.InvalidPosition);
        _service.UpdateEntry("a.md", position: 4).Outcome.Should().Be(UpdateEntryOutcome.InvalidPosition);
    }

    [Fact]
    public void UpdateEntry_UnknownPost_ReturnsNotScheduled()
    {
        _service.UpdateEntry("missing.md", title: "x").Outcome.Should().Be(UpdateEntryOutcome.NotScheduled);
    }

    [Fact]
    public void UpdateEntry_DatePrefixedPostName_IsNormalised()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");

        var result = _service.UpdateEntry("_posts/2026/2026-07-21-a.md", title: "A revised");

        result.Outcome.Should().Be(UpdateEntryOutcome.Updated);
        result.Entry!.DraftFilename.Should().Be("a.md");
        result.Entry.Title.Should().Be("A revised");
    }

    // ---- RemoveEntry ----

    [Fact]
    public void RemoveEntry_RemovesAndDenselyRenumbersRemaining()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");
        AddEntry(seriesId, "B", "b.md");
        AddEntry(seriesId, "C", "c.md");

        _service.RemoveEntry("b.md").Should().Be(RemoveEntryOutcome.Removed);

        _service.GetSeriesEntries("Alpha").Select(e => (e.DraftFilename, e.Position))
            .Should().Equal(("a.md", 1), ("c.md", 2));
    }

    [Fact]
    public void RemoveEntry_UnknownPost_ReturnsNotScheduled()
    {
        _service.RemoveEntry("missing.md").Should().Be(RemoveEntryOutcome.NotScheduled);
    }

    // ---- RenameSeries ----

    [Fact]
    public void RenameSeries_Renames()
    {
        _repository.AddSeries("Alpha");

        _service.RenameSeries("Alpha", "Alpha Renamed").Should().Be(RenameSeriesOutcome.Renamed);
        _service.ListSeries().Should().Contain(s => s.Name == "Alpha Renamed");
    }

    [Fact]
    public void RenameSeries_UnknownOldName_ReturnsNotFound()
    {
        _service.RenameSeries("Missing", "New").Should().Be(RenameSeriesOutcome.NotFound);
    }

    [Fact]
    public void RenameSeries_NewNameTakenByDifferentSeries_ReturnsNameTaken()
    {
        _repository.AddSeries("Alpha");
        _repository.AddSeries("Beta");

        _service.RenameSeries("Alpha", "beta").Should().Be(RenameSeriesOutcome.NameTaken);
    }

    [Fact]
    public void RenameSeries_CasingOnlyChangeOfSameSeries_IsAllowed()
    {
        _repository.AddSeries("Alpha");

        _service.RenameSeries("Alpha", "ALPHA").Should().Be(RenameSeriesOutcome.Renamed);
        _service.ListSeries().Should().Contain(s => s.Name == "ALPHA");
    }

    // ---- DeleteSeries ----

    [Fact]
    public void DeleteSeries_Empty_Deletes()
    {
        _repository.AddSeries("Alpha");

        _service.DeleteSeries("Alpha").Should().Be(DeleteSeriesOutcome.Deleted);
        _service.ListSeries().Should().NotContain(s => s.Name == "Alpha");
    }

    [Fact]
    public void DeleteSeries_NonEmpty_ReturnsNotEmpty_AndEntriesSurvive()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md");

        _service.DeleteSeries("Alpha").Should().Be(DeleteSeriesOutcome.NotEmpty);
        _service.ListSeries().Should().Contain(s => s.Name == "Alpha");
        _service.GetSeriesEntries("Alpha").Should().ContainSingle(e => e.DraftFilename == "a.md");
    }

    [Fact]
    public void DeleteSeries_Unknown_ReturnsNotFound()
    {
        _service.DeleteSeries("Missing").Should().Be(DeleteSeriesOutcome.NotFound);
    }

    // ---- SetSeriesCadence ----

    [Fact]
    public void SetSeriesCadence_SetsAndRoundTripsViaListSeries()
    {
        _repository.AddSeries("Alpha");

        _service.SetSeriesCadence("Alpha", DayOfWeek.Monday, new DateOnly(2026, 7, 6))
            .Should().Be(SetCadenceOutcome.Set);

        var series = _service.ListSeries().Single(s => s.Name == "Alpha");
        series.CadenceDay.Should().Be(DayOfWeek.Monday);
        series.CadenceStart.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void SetSeriesCadence_StartDateDayMismatch_ReturnsDayMismatch()
    {
        _repository.AddSeries("Alpha");

        // 2026-07-07 is a Tuesday, not a Monday.
        _service.SetSeriesCadence("Alpha", DayOfWeek.Monday, new DateOnly(2026, 7, 7))
            .Should().Be(SetCadenceOutcome.DayMismatch);
    }

    [Fact]
    public void SetSeriesCadence_UnknownSeries_ReturnsSeriesNotFound()
    {
        _service.SetSeriesCadence("Missing", DayOfWeek.Monday, new DateOnly(2026, 7, 6))
            .Should().Be(SetCadenceOutcome.SeriesNotFound);
    }

    // ---- GetDuePosts ----

    [Fact]
    public void GetDuePosts_ExplicitPublishDateInThePast_IsOverdueWithCorrectDaysOverdue()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 14));

        var due = _service.GetDuePosts();

        due.Should().ContainSingle();
        due[0].Entry.DraftFilename.Should().Be("a.md");
        due[0].DueDate.Should().Be(new DateOnly(2026, 7, 14));
        due[0].Overdue.Should().BeTrue();
        due[0].DaysOverdue.Should().Be(7);
    }

    [Fact]
    public void GetDuePosts_DueToday_IsNotOverdue()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 21));

        var due = _service.GetDuePosts();

        due.Should().ContainSingle();
        due[0].Overdue.Should().BeFalse();
        due[0].DaysOverdue.Should().Be(0);
    }

    [Fact]
    public void GetDuePosts_FutureDate_IsExcluded()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 22));

        _service.GetDuePosts().Should().BeEmpty();
    }

    [Fact]
    public void GetDuePosts_WeekNumberedEntry_ResolvedViaSeriesCadence()
    {
        var seriesId = _repository.AddSeries("Cadence");
        _service.SetSeriesCadence("Cadence", DayOfWeek.Monday, new DateOnly(2026, 7, 6));
        AddEntry(seriesId, "A", "a.md", week: 3);

        var due = _service.GetDuePosts();

        due.Should().ContainSingle();
        due[0].DueDate.Should().Be(new DateOnly(2026, 7, 20));
        due[0].Overdue.Should().BeTrue();
        due[0].DaysOverdue.Should().Be(1);
    }

    [Fact]
    public void GetDuePosts_WeekNumberedEntry_InSeriesWithoutCadence_IsExcluded()
    {
        var seriesId = _repository.AddSeries("NoCadence");
        AddEntry(seriesId, "A", "a.md", week: 3);

        _service.GetDuePosts().Should().BeEmpty();
    }

    [Fact]
    public void GetDuePosts_ExcludesPublishedEntries()
    {
        var seriesId = _repository.AddSeries("Alpha");
        AddEntry(seriesId, "A", "a.md", publishDate: new DateOnly(2026, 7, 1), published: true);

        _service.GetDuePosts().Should().BeEmpty();
    }

    [Fact]
    public void GetDuePosts_OrdersByDueDateThenSeriesThenPosition()
    {
        var alphaId = _repository.AddSeries("Alpha");
        AddEntry(alphaId, "A2", "a2.md", position: 2, publishDate: new DateOnly(2026, 7, 10));
        AddEntry(alphaId, "A1", "a1.md", position: 1, publishDate: new DateOnly(2026, 7, 10));
        var betaId = _repository.AddSeries("Beta");
        AddEntry(betaId, "B1", "b1.md", publishDate: new DateOnly(2026, 7, 5));

        var due = _service.GetDuePosts();

        due.Select(d => d.Entry.DraftFilename).Should().Equal("b1.md", "a1.md", "a2.md");
    }

    public void Dispose() => _db.Dispose();
}
