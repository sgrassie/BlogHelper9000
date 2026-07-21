using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ListDraftsToolTests
{
    private readonly IBlogService _blogService = Substitute.For<IBlogService>();
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static DraftDetail Draft(string fileName = "a-draft.md", string? title = "A Draft",
        int wordCount = 42, DateTime? lastModified = null, bool hasFeaturedImage = false,
        IReadOnlyList<string>? readinessFlags = null) =>
        new(fileName, $"/blog/_drafts/{fileName}", title, wordCount,
            lastModified ?? new DateTime(2026, 7, 1), hasFeaturedImage, readinessFlags ?? []);

    private static ScheduleEntry Entry(string series = "Series", int? week = null, DateOnly? publishDate = null) =>
        new(1, series, 1, week, publishDate, "Topic", "Title", "a-draft.md", "csharp", "New", false, null, null);

    [Fact]
    public void ListDrafts_WhenNoDrafts_ReturnsSuccessWithEmptyListAndZeroTotal()
    {
        _blogService.GetDraftDetails().Returns(new List<DraftDetail>());

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        result.Success.Should().BeTrue();
        result.Data!.Drafts.Should().BeEmpty();
        result.Data.Total.Should().Be(0);
    }

    [Fact]
    public void ListDrafts_ScheduledWithPublishDate_MapsScheduleSlotToTheDate()
    {
        _blogService.GetDraftDetails().Returns([Draft()]);
        _scheduleService.FindEntry("a-draft.md").Returns(Entry(publishDate: new DateOnly(2026, 7, 9)));

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        var dto = result.Data!.Drafts.Single();
        dto.Series.Should().Be("Series");
        dto.ScheduleSlot.Should().Be("2026-07-09");
    }

    [Fact]
    public void ListDrafts_ScheduledWithWeekOnly_MapsScheduleSlotToWeekLabel()
    {
        _blogService.GetDraftDetails().Returns([Draft()]);
        _scheduleService.FindEntry("a-draft.md").Returns(Entry(week: 3, publishDate: null));

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        result.Data!.Drafts.Single().ScheduleSlot.Should().Be("Week 3");
    }

    [Fact]
    public void ListDrafts_Unscheduled_LeavesSeriesAndSlotNull()
    {
        _blogService.GetDraftDetails().Returns([Draft()]);
        _scheduleService.FindEntry("a-draft.md").Returns((ScheduleEntry?)null);

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        var dto = result.Data!.Drafts.Single();
        dto.Series.Should().BeNull();
        dto.ScheduleSlot.Should().BeNull();
    }

    [Fact]
    public void ListDrafts_WhenNoScheduleDatabase_StillSucceeds_WithNullScheduleFields()
    {
        _blogService.GetDraftDetails().Returns([Draft()]);
        _scheduleService.DatabaseExists.Returns(false);
        _scheduleService.FindEntry("a-draft.md").Returns((ScheduleEntry?)null);

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        result.Success.Should().BeTrue();
        var dto = result.Data!.Drafts.Single();
        dto.Series.Should().BeNull();
        dto.ScheduleSlot.Should().BeNull();
    }

    [Fact]
    public void ListDrafts_AppliesLimit_ButTotalReflectsFullCount()
    {
        var drafts = Enumerable.Range(1, 5)
            .Select(i => Draft($"draft-{i}.md"))
            .ToList();
        _blogService.GetDraftDetails().Returns(drafts);
        _scheduleService.FindEntry(Arg.Any<string>()).Returns((ScheduleEntry?)null);

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService, limit: 2);

        result.Data!.Drafts.Should().HaveCount(2);
        result.Data.Total.Should().Be(5);
    }

    [Fact]
    public void ListDrafts_MapsWordCountTitleAndReadinessFlags()
    {
        _blogService.GetDraftDetails().Returns([
            Draft("a-draft.md", "A Draft", wordCount: 123, readinessFlags: ["TODO", "[placeholder"])
        ]);
        _scheduleService.FindEntry("a-draft.md").Returns((ScheduleEntry?)null);

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        var dto = result.Data!.Drafts.Single();
        dto.Title.Should().Be("A Draft");
        dto.WordCount.Should().Be(123);
        dto.ReadinessFlags.Should().Equal("TODO", "[placeholder");
    }

    [Fact]
    public void ListDrafts_ReportsHasFeaturedImage_PerDraft()
    {
        _blogService.GetDraftDetails().Returns([
            Draft("with-image.md", "With Image", hasFeaturedImage: true),
            Draft("without-image.md", "Without Image", hasFeaturedImage: false)
        ]);
        _scheduleService.FindEntry(Arg.Any<string>()).Returns((ScheduleEntry?)null);

        var result = ListDraftsTool.ListDrafts(_blogService, _scheduleService);

        result.Data!.Drafts.Should().ContainSingle(d => d.Title == "With Image" && d.HasFeaturedImage);
        result.Data.Drafts.Should().ContainSingle(d => d.Title == "Without Image" && !d.HasFeaturedImage);
    }
}
