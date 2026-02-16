using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class PublishPostToolTests
{
    [Fact]
    public void PublishPost_WhenPostExists_ReturnsSuccessMessage()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.PublishPost("my-draft")
            .Returns("/path/to/_posts/2024/my-draft.md");

        // Act
        var result = PublishPostTool.PublishPost(blogService, "my-draft");

        // Assert
        result.Should().Contain("Published to: /path/to/_posts/2024/my-draft.md");
        blogService.Received(1).PublishPost("my-draft");
    }

    [Fact]
    public void PublishPost_WhenPostNotFound_ReturnsErrorMessage()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.PublishPost("nonexistent")
            .Returns((string?)null);

        // Act
        var result = PublishPostTool.PublishPost(blogService, "nonexistent");

        // Assert
        result.Should().Contain("Could not find post 'nonexistent' to publish.");
        blogService.Received(1).PublishPost("nonexistent");
    }
}
