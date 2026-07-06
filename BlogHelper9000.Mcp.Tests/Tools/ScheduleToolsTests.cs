using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ScheduleToolsTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static ScheduleEntry Entry(string series = "Series", int position = 1, bool published = false) =>
        new(1, series, position, 2, new DateOnly(2026, 7, 7), "Topic", "Title", "title.md",
            "csharp", "New", published, null, null);

    public ScheduleToolsTests() => _scheduleService.DatabaseExists.Returns(true);

    [Fact]
    public void AllScheduleTools_WhenNoDatabase_FailWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        ListSeriesTool.ListSeries(_scheduleService).Success.Should().BeFalse();
        GetSeriesTool.GetSeries(_scheduleService, "x").Success.Should().BeFalse();
        GetScheduleStatsTool.GetScheduleStats(_scheduleService).Success.Should().BeFalse();
        GetNextScheduledPostTool.GetNextScheduledPost(_scheduleService, null).Success.Should().BeFalse();

        ListSeriesTool.ListSeries(_scheduleService).Error.Should().Contain("schedule-import");
    }

    [Fact]
    public void ListSeries_MapsDashboardSeriesStats()
    {
        _scheduleService.GetDashboard().Returns(new ScheduleDashboard(243, null, 1, 244, 2, 1, 0.5,
            [new SeriesStats("S", 2, 1, 1, 0.5, "A", "B", "Week 2", null)]));

        var result = ListSeriesTool.ListSeries(_scheduleService);

        result.Success.Should().BeTrue();
        result.Data!.Series.Should().ContainSingle(s => s.Series == "S" && s.NextSlot == "Week 2");
    }

    [Fact]
    public void GetSeries_UnknownSeries_Fails()
    {
        _scheduleService.GetSeriesEntries("nope").Returns([]);

        GetSeriesTool.GetSeries(_scheduleService, "nope").Success.Should().BeFalse();
    }

    [Fact]
    public void GetSeries_MapsEntries()
    {
        _scheduleService.GetSeriesEntries("Series").Returns([Entry()]);

        var result = GetSeriesTool.GetSeries(_scheduleService, "Series");

        result.Success.Should().BeTrue();
        result.Data!.Entries.Should().ContainSingle(e =>
            e.DraftFilename == "title.md" && e.PublishDate == new DateOnly(2026, 7, 7));
    }

    [Fact]
    public void GetScheduleStats_MapsTheDashboard()
    {
        _scheduleService.GetDashboard().Returns(new ScheduleDashboard(
            243, new DateOnly(2026, 7, 4), 1, 244, 189, 188, 1.0 / 189, []));

        var result = GetScheduleStatsTool.GetScheduleStats(_scheduleService);

        result.Success.Should().BeTrue();
        result.Data!.TotalPublished.Should().Be(244);
        result.Data.TotalRemaining.Should().Be(188);
    }

    [Fact]
    public void AddPostToSeries_ReturnsThePlacedEntry()
    {
        _scheduleService.AddToSeries("Series", "Title", "title.md", 2, null, "csharp", null)
            .Returns(Entry());

        var result = AddPostToSeriesTool.AddPostToSeries(_scheduleService, "Series", "Title", "title.md",
            week: 2, tags: "csharp");

        result.Success.Should().BeTrue();
        result.Data!.Position.Should().Be(1);
    }

    [Fact]
    public void AddPostToSeries_DuplicateFilename_Fails()
    {
        _scheduleService.AddToSeries("Series", "Title", "title.md", null, null, null, null)
            .Returns((ScheduleEntry?)null);

        AddPostToSeriesTool.AddPostToSeries(_scheduleService, "Series", "Title", "title.md")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void AddPostToSeries_BadDate_Fails()
    {
        AddPostToSeriesTool.AddPostToSeries(_scheduleService, "Series", "Title", "title.md",
            publishDate: "not-a-date").Success.Should().BeFalse();
    }

    [Fact]
    public void MarkScheduleEntryPublished_ReportsTheOutcome()
    {
        _scheduleService.MarkPublished("title.md", null, false).Returns(MarkPublishedOutcome.Marked);

        var result = MarkScheduleEntryPublishedTool.MarkScheduleEntryPublished(_scheduleService, "title.md");

        result.Success.Should().BeTrue();
        result.Data!.Outcome.Should().Be("Marked");
    }

    [Fact]
    public void MarkScheduleEntryPublished_NotScheduled_Fails()
    {
        _scheduleService.MarkPublished("nope.md", null, false).Returns(MarkPublishedOutcome.NotScheduled);

        MarkScheduleEntryPublishedTool.MarkScheduleEntryPublished(_scheduleService, "nope.md")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void GetNextScheduledPost_ReturnsTheNextEntry()
    {
        _scheduleService.GetNextUnpublished(null).Returns(Entry());

        var result = GetNextScheduledPostTool.GetNextScheduledPost(_scheduleService, null);

        result.Success.Should().BeTrue();
        result.Data!.DraftFilename.Should().Be("title.md");
    }

    [Fact]
    public void GetNextScheduledPost_WhenAllPublished_Fails()
    {
        _scheduleService.GetNextUnpublished("Series").Returns((ScheduleEntry?)null);

        GetNextScheduledPostTool.GetNextScheduledPost(_scheduleService, "Series")
            .Success.Should().BeFalse();
    }
}
