using BlogHelper9000.Core.Models;
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
        var result = PublishPostTool.PublishPost(blogService, "my-draft");

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
        var result = PublishPostTool.PublishPost(blogService, "nonexistent");

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
        var result = PublishPostTool.PublishPost(blogService, "2024-01-01-a-post.md");

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
        var result = PublishPostTool.PublishPost(blogService, "a-post.md");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("target path");
    }
}
