using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class UnpublishPostToolTests
{
    [Fact]
    public void UnpublishPost_RealRun_WhenScheduledAndDatabaseExists_UnmarksSchedule()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", false)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.MarkPublished("2024-01-01-a-post.md", unmark: true)
            .Returns(MarkPublishedOutcome.Unmarked);

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md", dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.DraftPath.Should().Be("/blog/_drafts/a-post.md");
        result.Data!.Outcome.Should().Be("Unpublished");
        result.Data!.ScheduleOutcome.Should().Be("Unmarked");
        result.Data!.DryRun.Should().BeFalse();
        blogService.Received(1).UnpublishPostDetailed("2024-01-01-a-post.md", false);
        scheduleService.Received(1).MarkPublished("2024-01-01-a-post.md", unmark: true);
    }

    [Fact]
    public void UnpublishPost_RealRun_WhenNotScheduled_MapsToNotScheduled()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", false)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.MarkPublished("2024-01-01-a-post.md", unmark: true)
            .Returns(MarkPublishedOutcome.NotScheduled);

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md", dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().Be("NotScheduled");
    }

    [Fact]
    public void UnpublishPost_DryRunDefault_WhenScheduledEntryPublished_PreviewsWouldUnmark()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.FindEntry("2024-01-01-a-post.md")
            .Returns(new ScheduleEntry(1, "FootballData", 3, null, null, null, "A Post",
                "a-post.md", null, null, true, null, null));

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.DryRun.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().Be("WouldUnmark");
        blogService.Received(1).UnpublishPostDetailed("2024-01-01-a-post.md", true);
        scheduleService.DidNotReceive().MarkPublished(Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<bool>());
    }

    [Fact]
    public void UnpublishPost_DryRunDefault_WhenScheduledEntryNotYetPublished_StillPreviewsWouldUnmark()
    {
        // Arrange — MarkPublished(unmark: true) unmarks ANY found entry regardless of its
        // current Published state, so the dry-run preview must match: any found entry
        // previews "WouldUnmark", not just ones already marked Published.
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.FindEntry("2024-01-01-a-post.md")
            .Returns(new ScheduleEntry(1, "FootballData", 3, null, null, null, "A Post",
                "a-post.md", null, null, false, null, null));

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().Be("WouldUnmark");
    }

    [Fact]
    public void UnpublishPost_DryRunDefault_WhenEntryNotFound_PreviewsNotScheduled()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(true);
        scheduleService.FindEntry("2024-01-01-a-post.md").Returns((ScheduleEntry?)null);

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().Be("NotScheduled");
    }

    [Fact]
    public void UnpublishPost_WhenNotFound_ReturnsDistinctFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("missing.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.NotFound, null));

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "missing.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("missing.md");
        _ = scheduleService.DidNotReceive().DatabaseExists;
    }

    [Fact]
    public void UnpublishPost_WhenNotPublished_ReturnsFailureExplainingDraftsCannotBeUnpublished()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("my-draft.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.NotPublished, null));

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "my-draft.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not a published post");
        result.Error.Should().Contain("drafts cannot be unpublished");
    }

    [Fact]
    public void UnpublishPost_WhenTargetExists_ReturnsFailureNamingTheTarget()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.TargetExists, "/blog/_drafts/a-post.md"));

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("a draft named");
        result.Error.Should().Contain("/blog/_drafts/a-post.md");
    }

    [Fact]
    public void UnpublishPost_RealRun_WhenNoScheduleDatabase_SucceedsWithoutTouchingSchedule()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", false)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(false);

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md", dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().BeNull();
        scheduleService.DidNotReceive().MarkPublished(Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<bool>());
        scheduleService.DidNotReceive().FindEntry(Arg.Any<string>());
    }

    [Fact]
    public void UnpublishPost_DryRun_WhenNoScheduleDatabase_ScheduleOutcomeIsNull()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.UnpublishPostDetailed("2024-01-01-a-post.md", true)
            .Returns(new UnpublishPostResult(UnpublishOutcome.Unpublished, "/blog/_drafts/a-post.md"));
        scheduleService.DatabaseExists.Returns(false);

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().BeNull();
        scheduleService.DidNotReceive().FindEntry(Arg.Any<string>());
    }

    // Pattern (a) integration test: wired to the REAL BlogService (MockFileSystem + real
    // PostManager), with NSubstitute only for IScheduleService. This is the collision test
    // that a fully-mocked IBlogService can't catch: the mock in
    // UnpublishPost_WhenTargetExists_ReturnsFailureNamingTheTarget above encodes DraftPath as
    // non-null by hand, which stayed green even when BlogService.UnpublishPostDetailed itself
    // returned null for that field. Exercising the real service closes that mock-drift hole.
    [Fact]
    public void UnpublishPost_RealBlogService_WhenTargetExists_MessageContainsTheRealDraftPath()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_posts/2024/2024-11-01-a-post.md",
            new MockFileData("---\ntitle: A post\npublished: 01/11/2024\nispublished: true\n---"));
        fileSystem.AddFile("/blog/_drafts/a-post.md", new MockFileData("---\ntitle: Existing draft\n---"));

        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem),
            Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);
        var scheduleService = Substitute.For<IScheduleService>();

        // Act
        var result = UnpublishPostTool.UnpublishPost(blogService, scheduleService, "2024-11-01-a-post.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("a draft named '/blog/_drafts/a-post.md' already exists");
    }
}
