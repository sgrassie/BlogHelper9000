using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class AddPostToolTests
{
    [Fact]
    public void AddPost_CreatesDraft_ReturnsSuccessMessage()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.AddPost("Test Post", true, false, false, null)
            .Returns("/path/to/_drafts/test-post.md");

        // Act
        var result = AddPostTool.AddPost(blogService, "Test Post", isDraft: true);

        // Assert
        result.Should().Contain("Created draft at: /path/to/_drafts/test-post.md");
        blogService.Received(1).AddPost("Test Post", true, false, false, null);
    }

    [Fact]
    public void AddPost_CreatesPublishedPost_ReturnsSuccessMessage()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.AddPost("Test Post", false, true, false, "/path/image.jpg")
            .Returns("/path/to/_posts/2024/test-post.md");

        // Act
        var result = AddPostTool.AddPost(blogService, "Test Post", isDraft: false, isFeatured: true, featuredImage: "/path/image.jpg");

        // Assert
        result.Should().Contain("Created post at: /path/to/_posts/2024/test-post.md");
        blogService.Received(1).AddPost("Test Post", false, true, false, "/path/image.jpg");
    }
}
