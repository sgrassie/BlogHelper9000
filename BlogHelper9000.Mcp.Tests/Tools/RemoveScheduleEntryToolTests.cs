using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class RemoveScheduleEntryToolTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static ScheduleEntry Entry(string series = "Series", int position = 3) =>
        new(1, series, position, 2, new DateOnly(2026, 7, 7), "Topic", "Title", "title.md",
            "csharp", "New", false, null, null);

    public RemoveScheduleEntryToolTests() => _scheduleService.DatabaseExists.Returns(true);

    [Fact]
    public void RemoveScheduleEntry_NotFound_Fails()
    {
        _scheduleService.FindEntry("nope.md").Returns((ScheduleEntry?)null);

        var result = RemoveScheduleEntryTool.RemoveScheduleEntry(_scheduleService, "nope.md");

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceive().RemoveEntry(Arg.Any<string>());
    }

    [Fact]
    public void RemoveScheduleEntry_DryRun_PreviewsWithoutRemoving()
    {
        _scheduleService.FindEntry("title.md").Returns(Entry());

        var result = RemoveScheduleEntryTool.RemoveScheduleEntry(_scheduleService, "title.md");

        result.Success.Should().BeTrue();
        result.Data!.DryRun.Should().BeTrue();
        result.Data.Removed.Should().BeFalse();
        result.Data.Series.Should().Be("Series");
        result.Data.Position.Should().Be(3);
        result.Data.DraftFilename.Should().Be("title.md");
        _scheduleService.DidNotReceive().RemoveEntry(Arg.Any<string>());
    }

    [Fact]
    public void RemoveScheduleEntry_RealRun_Removes()
    {
        _scheduleService.FindEntry("title.md").Returns(Entry());
        _scheduleService.RemoveEntry("title.md").Returns(RemoveEntryOutcome.Removed);

        var result = RemoveScheduleEntryTool.RemoveScheduleEntry(_scheduleService, "title.md", dryRun: false);

        result.Success.Should().BeTrue();
        result.Data!.DryRun.Should().BeFalse();
        result.Data.Removed.Should().BeTrue();
        _scheduleService.Received(1).RemoveEntry("title.md");
    }

    [Fact]
    public void RemoveScheduleEntry_RealRun_WhenNotFound_FailsWithoutCallingRemove()
    {
        _scheduleService.FindEntry("nope.md").Returns((ScheduleEntry?)null);

        var result = RemoveScheduleEntryTool.RemoveScheduleEntry(_scheduleService, "nope.md", dryRun: false);

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceive().RemoveEntry(Arg.Any<string>());
    }

    [Fact]
    public void RemoveScheduleEntry_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        RemoveScheduleEntryTool.RemoveScheduleEntry(_scheduleService, "title.md")
            .Error.Should().Contain("schedule-import");
    }
}
