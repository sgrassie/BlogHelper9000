using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class GetDuePostsToolTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static ScheduleEntry Entry(string series = "Series", int position = 2) =>
        new(1, series, position, 3, null, "Topic", "Title", "title.md",
            "csharp", "New", false, null, null);

    public GetDuePostsToolTests() => _scheduleService.DatabaseExists.Returns(true);

    [Fact]
    public void GetDuePosts_EmptyList_SucceedsWithNoEntries()
    {
        _scheduleService.GetDuePosts(null).Returns([]);

        var result = GetDuePostsTool.GetDuePosts(_scheduleService, null);

        result.Success.Should().BeTrue();
        result.Data!.Due.Should().BeEmpty();
    }

    [Fact]
    public void GetDuePosts_MapsEntries()
    {
        var due = new DuePost(Entry(), new DateOnly(2026, 7, 20), true, 1);
        _scheduleService.GetDuePosts(null).Returns([due]);

        var result = GetDuePostsTool.GetDuePosts(_scheduleService, null);

        result.Success.Should().BeTrue();
        result.Data!.Due.Should().ContainSingle(d =>
            d.Series == "Series" && d.Title == "Title" && d.DraftFilename == "title.md" &&
            d.DueDate == "2026-07-20" && d.Overdue && d.DaysOverdue == 1 && d.Week == 3 && d.Position == 2);
    }

    [Fact]
    public void GetDuePosts_ParsesAsOfDate()
    {
        _scheduleService.GetDuePosts(new DateOnly(2026, 7, 1)).Returns([]);

        var result = GetDuePostsTool.GetDuePosts(_scheduleService, "2026-07-01");

        result.Success.Should().BeTrue();
        _scheduleService.Received(1).GetDuePosts(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void GetDuePosts_BadAsOfDate_Fails()
    {
        GetDuePostsTool.GetDuePosts(_scheduleService, "not-a-date").Success.Should().BeFalse();
    }

    [Fact]
    public void GetDuePosts_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        GetDuePostsTool.GetDuePosts(_scheduleService, null).Error.Should().Contain("schedule-import");
    }
}
