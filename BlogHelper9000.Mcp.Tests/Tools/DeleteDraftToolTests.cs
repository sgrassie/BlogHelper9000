using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class DeleteDraftToolTests
{
    [Fact]
    public void DeleteDraft_RealRun_WhenScheduledAndDatabaseExists_RemovesEntryAndDescribesIt()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.DeleteDraft("my-draft.md", false)
            .Returns(new DeleteDraftResult(DeleteDraftOutcome.Deleted, "/blog/_drafts/my-draft.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.FindEntry("my-draft.md")
            .Returns(new ScheduleEntry(1, "FootballData", 3, null, null, null, "My Draft",
                "my-draft.md", null, null, false, null, null));

        // Act
        var result = DeleteDraftTool.DeleteDraft(blogService, scheduleService, "my-draft.md", dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.FilePath.Should().Be("/blog/_drafts/my-draft.md");
        result.Data!.DryRun.Should().BeFalse();
        result.Data!.Deleted.Should().BeTrue();
        result.Data!.ScheduleEntry.Should().Be("Series 'FootballData' position 3");
        blogService.Received(1).DeleteDraft("my-draft.md", false);
        scheduleService.Received(1).RemoveEntry("my-draft.md");
    }

    [Fact]
    public void DeleteDraft_RealRun_WhenNotScheduled_ScheduleEntryIsNullAndRemoveEntryNotCalled()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.DeleteDraft("my-draft.md", false)
            .Returns(new DeleteDraftResult(DeleteDraftOutcome.Deleted, "/blog/_drafts/my-draft.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.FindEntry("my-draft.md").Returns((ScheduleEntry?)null);

        // Act
        var result = DeleteDraftTool.DeleteDraft(blogService, scheduleService, "my-draft.md", dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleEntry.Should().BeNull();
        scheduleService.DidNotReceive().RemoveEntry(Arg.Any<string>());
    }

    [Fact]
    public void DeleteDraft_DryRunDefault_CallsBlogServiceWithDryRunTrueAndDoesNotRemoveEntry()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.DeleteDraft("my-draft.md", true)
            .Returns(new DeleteDraftResult(DeleteDraftOutcome.Deleted, "/blog/_drafts/my-draft.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.FindEntry("my-draft.md")
            .Returns(new ScheduleEntry(1, "FootballData", 3, null, null, null, "My Draft",
                "my-draft.md", null, null, false, null, null));

        // Act
        var result = DeleteDraftTool.DeleteDraft(blogService, scheduleService, "my-draft.md");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.DryRun.Should().BeTrue();
        result.Data!.Deleted.Should().BeFalse();
        result.Data!.ScheduleEntry.Should().Be("Series 'FootballData' position 3");
        blogService.Received(1).DeleteDraft("my-draft.md", true);
        scheduleService.DidNotReceive().RemoveEntry(Arg.Any<string>());
    }

    [Fact]
    public void DeleteDraft_WhenNotFound_ReturnsDistinctFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.DeleteDraft("missing.md", true)
            .Returns(new DeleteDraftResult(DeleteDraftOutcome.NotFound, null));

        // Act
        var result = DeleteDraftTool.DeleteDraft(blogService, scheduleService, "missing.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Could not find draft 'missing.md'");
        _ = scheduleService.DidNotReceive().DatabaseExists;
    }

    [Fact]
    public void DeleteDraft_WhenNotADraft_ReturnsFailureMentioningUnpublish()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.DeleteDraft("2024-01-01-a-post.md", true)
            .Returns(new DeleteDraftResult(DeleteDraftOutcome.NotADraft, null));

        // Act
        var result = DeleteDraftTool.DeleteDraft(blogService, scheduleService, "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("draft");
        result.Error.Should().Contain("unpublish_post");
    }

    [Fact]
    public void DeleteDraft_WhenNoScheduleDatabase_SucceedsWithoutTouchingSchedule()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.DeleteDraft("my-draft.md", false)
            .Returns(new DeleteDraftResult(DeleteDraftOutcome.Deleted, "/blog/_drafts/my-draft.md"));
        scheduleService.DatabaseExists.Returns(false);

        // Act
        var result = DeleteDraftTool.DeleteDraft(blogService, scheduleService, "my-draft.md", dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleEntry.Should().BeNull();
        scheduleService.DidNotReceive().FindEntry(Arg.Any<string>());
        scheduleService.DidNotReceive().RemoveEntry(Arg.Any<string>());
    }
}
