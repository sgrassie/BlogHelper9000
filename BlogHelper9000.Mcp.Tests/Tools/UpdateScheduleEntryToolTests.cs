using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class UpdateScheduleEntryToolTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static ScheduleEntry Entry(string series = "Series", int position = 1) =>
        new(1, series, position, 2, new DateOnly(2026, 7, 7), "Topic", "Title", "title.md",
            "csharp", "New", false, null, null);

    public UpdateScheduleEntryToolTests() => _scheduleService.DatabaseExists.Returns(true);

    [Fact]
    public void UpdateScheduleEntry_Updated_ReturnsMappedEntry()
    {
        var entry = Entry();
        _scheduleService.UpdateEntry("title.md", null, null, 3, null, null, null, null, false, false)
            .Returns(new UpdateEntryResult(UpdateEntryOutcome.Updated, entry));

        var result = UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", week: 3);

        result.Success.Should().BeTrue();
        result.Data!.Series.Should().Be("Series");
        result.Data.Entry.DraftFilename.Should().Be("title.md");
        result.Data.Entry.Week.Should().Be(2);
    }

    [Fact]
    public void UpdateScheduleEntry_NotScheduled_Fails()
    {
        _scheduleService.UpdateEntry("nope.md", null, null, 3, null, null, null, null, false, false)
            .Returns(new UpdateEntryResult(UpdateEntryOutcome.NotScheduled, null));

        UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "nope.md", week: 3)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void UpdateScheduleEntry_SeriesNotFound_Fails()
    {
        _scheduleService.UpdateEntry("title.md", "Ghost", null, null, null, null, null, null, false, false)
            .Returns(new UpdateEntryResult(UpdateEntryOutcome.SeriesNotFound, null));

        UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", series: "Ghost")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void UpdateScheduleEntry_InvalidPosition_Fails()
    {
        _scheduleService.UpdateEntry("title.md", null, 99, null, null, null, null, null, false, false)
            .Returns(new UpdateEntryResult(UpdateEntryOutcome.InvalidPosition, null));

        UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", position: 99)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void UpdateScheduleEntry_BadPublishDate_FailsWithoutCallingService()
    {
        var result = UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", publishDate: "not-a-date");

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().UpdateEntry(default!);
    }

    [Fact]
    public void UpdateScheduleEntry_NothingSupplied_FailsWithoutCallingService()
    {
        var result = UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md");

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().UpdateEntry(default!);
    }

    [Fact]
    public void UpdateScheduleEntry_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", week: 1)
            .Error.Should().Contain("schedule-import");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateScheduleEntry_WeekLessThanOne_FailsWithoutCallingService(int week)
    {
        var result = UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", week: week);

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().UpdateEntry(default!);
    }

    [Fact]
    public void UpdateScheduleEntry_WeekAndClearWeekBothSupplied_FailsWithoutCallingService()
    {
        var result = UpdateScheduleEntryTool.UpdateScheduleEntry(_scheduleService, "title.md", week: 3, clearWeek: true);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("week").And.Contain("clearWeek");
        _scheduleService.DidNotReceiveWithAnyArgs().UpdateEntry(default!);
    }

    [Fact]
    public void UpdateScheduleEntry_PublishDateAndClearPublishDateBothSupplied_FailsWithoutCallingService()
    {
        var result = UpdateScheduleEntryTool.UpdateScheduleEntry(
            _scheduleService, "title.md", publishDate: "2026-07-21", clearPublishDate: true);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("publishDate").And.Contain("clearPublishDate");
        _scheduleService.DidNotReceiveWithAnyArgs().UpdateEntry(default!);
    }
}
