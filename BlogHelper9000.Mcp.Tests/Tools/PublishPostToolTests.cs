using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class PublishPostToolTests
{
    [Fact]
    public void PublishPost_WhenPostExists_ReturnsSuccessEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.PublishPostDetailed("my-draft")
            .Returns(new PublishPostResult(PublishOutcome.Published, "/path/to/_posts/2024/my-draft.md"));

        // Act
        var result = PublishPostTool.PublishPost(blogService, Substitute.For<IScheduleService>(), "my-draft");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.PublishedPath.Should().Be("/path/to/_posts/2024/my-draft.md");
        blogService.Received(1).PublishPostDetailed("my-draft");
    }

    [Fact]
    public void PublishPost_WhenPostNotFound_ReturnsFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.PublishPostDetailed("nonexistent")
            .Returns(new PublishPostResult(PublishOutcome.NotFound, null));

        // Act
        var result = PublishPostTool.PublishPost(blogService, Substitute.For<IScheduleService>(), "nonexistent");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No draft named 'nonexistent'");
    }

    [Fact]
    public void PublishPost_WhenAlreadyPublished_ReturnsDistinctFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.PublishPostDetailed("2024-01-01-a-post.md")
            .Returns(new PublishPostResult(PublishOutcome.AlreadyPublished, null));

        // Act
        var result = PublishPostTool.PublishPost(blogService, Substitute.For<IScheduleService>(), "2024-01-01-a-post.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already");
        result.Error.Should().NotContain("No draft named");
    }

    [Fact]
    public void PublishPost_WhenTargetCollides_ReturnsDistinctFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.PublishPostDetailed("a-post.md")
            .Returns(new PublishPostResult(PublishOutcome.TargetExists, null));

        // Act
        var result = PublishPostTool.PublishPost(blogService, Substitute.For<IScheduleService>(), "a-post.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("target path");
    }

    [Fact]
    public void PublishPost_OnSuccess_TicksTheSchedule()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var scheduleService = Substitute.For<IScheduleService>();
        blogService.PublishPostDetailed("my-draft")
            .Returns(new PublishPostResult(PublishOutcome.Published, "/path/to/_posts/2026/my-draft.md"));
        scheduleService.MarkPublished("my-draft").Returns(MarkPublishedOutcome.Marked);

        // Act
        var result = PublishPostTool.PublishPost(blogService, scheduleService, "my-draft");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ScheduleOutcome.Should().Be("Marked");
    }
}
