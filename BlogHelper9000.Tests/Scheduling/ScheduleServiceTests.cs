using BlogHelper9000.Core.Scheduling;
using Microsoft.Extensions.Time.Testing;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleServiceTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly ScheduleService _service;

    public ScheduleServiceTests()
    {
        _service = new ScheduleService(_db, _time);
        var repository = new ScheduleRepository(_db);
        repository.SetMeta("baseline_published_count", "243");
        repository.SetMeta("baseline_date", "2026-07-04");
        var seriesId = repository.AddSeries("Traefik in the homelab");
        repository.AddEntry(seriesId, new NewScheduleEntry(1, 1, new DateOnly(2026, 7, 9), "Foundations",
            "Traefik in the homelab: one proxy for everything",
            "traefik-in-the-homelab-one-proxy-for-everything.md", "traefik, homelab", "New", false, null));
        repository.AddEntry(seriesId, new NewScheduleEntry(2, 2, new DateOnly(2026, 7, 16), "Foundations",
            "Routers, services, and entrypoints",
            "routers-services-and-entrypoints.md", "traefik, homelab", "New", false, null));
    }

    [Fact]
    public void GetDashboard_CombinesBaselineAndSeriesStats()
    {
        var dashboard = _service.GetDashboard();

        dashboard.Should().BeEquivalentTo(new
        {
            BaselinePublished = 243,
            BaselineDate = new DateOnly(2026, 7, 4),
            PublishedViaSchedules = 0,
            TotalPublished = 243,
            TotalPlanned = 2,
            TotalRemaining = 2,
            Progress = 0.0
        }, options => options.ExcludingMissingMembers());
        dashboard.Series.Should().ContainSingle(s => s.Series == "Traefik in the homelab");
    }

    [Fact]
    public void MarkPublished_NormalisesThePostName_AndStampsToday()
    {
        var outcome = _service.MarkPublished("_posts/2026/2026-07-06-routers-services-and-entrypoints.md");

        outcome.Should().Be(MarkPublishedOutcome.Marked);
        var entry = _service.GetSeriesEntries("Traefik in the homelab")
            .Single(e => e.DraftFilename == "routers-services-and-entrypoints.md");
        entry.Published.Should().BeTrue();
        entry.PublishedOn.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void MarkPublished_WhenAlreadyTicked_ReportsAlreadyMarked()
    {
        _service.MarkPublished("routers-services-and-entrypoints");

        _service.MarkPublished("routers-services-and-entrypoints")
            .Should().Be(MarkPublishedOutcome.AlreadyMarked);
    }

    [Fact]
    public void MarkPublished_UnknownPost_ReportsNotScheduled()
    {
        _service.MarkPublished("not-on-the-schedule.md").Should().Be(MarkPublishedOutcome.NotScheduled);
    }

    [Fact]
    public void MarkPublished_WithUnmark_ClearsTheTick()
    {
        _service.MarkPublished("routers-services-and-entrypoints.md");

        _service.MarkPublished("routers-services-and-entrypoints.md", unmark: true)
            .Should().Be(MarkPublishedOutcome.Unmarked);
        _service.GetSeriesEntries("Traefik in the homelab")
            .Single(e => e.DraftFilename == "routers-services-and-entrypoints.md")
            .Published.Should().BeFalse();
    }

    [Fact]
    public void AddToSeries_CreatesMissingSeries_AndAppends()
    {
        var entry = _service.AddToSeries("Brand new series", "A post", "a-post.md",
            week: 2, tags: "csharp");

        entry.Should().NotBeNull();
        entry!.Position.Should().Be(1);
        _service.ListSeries().Should().Contain(s => s.Name == "Brand new series");
    }

    [Fact]
    public void AddToSeries_DuplicateFilename_ReturnsNull()
    {
        _service.AddToSeries("Traefik in the homelab", "Duplicate",
            "routers-services-and-entrypoints.md").Should().BeNull();
    }

    [Fact]
    public void GetNextUnpublished_ForASeries_ReturnsFirstUntickedByPosition()
    {
        _service.MarkPublished("traefik-in-the-homelab-one-proxy-for-everything.md");

        _service.GetNextUnpublished("Traefik in the homelab")!
            .DraftFilename.Should().Be("routers-services-and-entrypoints.md");
    }

    [Fact]
    public void GetNextUnpublished_AcrossAllSeries_PrefersEarliestPlannedDate()
    {
        _service.AddToSeries("Undated", "Undated post", "undated.md");

        _service.GetNextUnpublished()!
            .DraftFilename.Should().Be("traefik-in-the-homelab-one-proxy-for-everything.md");
    }

    public void Dispose() => _db.Dispose();
}
