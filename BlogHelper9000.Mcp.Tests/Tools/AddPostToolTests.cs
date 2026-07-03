using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class AddPostToolTests
{
    [Fact]
    public void AddPost_CreatesDraft_ReturnsSuccessEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.AddPost("Test Post", true, false, false, null, null, null)
            .Returns("/path/to/_drafts/test-post.md");

        // Act
        var result = AddPostTool.AddPost(blogService, "Test Post", isDraft: true);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.FilePath.Should().Be("/path/to/_drafts/test-post.md");
        result.Data.IsDraft.Should().BeTrue();
        blogService.Received(1).AddPost("Test Post", true, false, false, null, null, null);
    }

    [Fact]
    public void AddPost_CreatesPublishedPost_ReturnsSuccessEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.AddPost("Test Post", false, true, false, "/path/image.jpg", null, null)
            .Returns("/path/to/_posts/2024/test-post.md");

        // Act
        var result = AddPostTool.AddPost(blogService, "Test Post", isDraft: false, isFeatured: true, featuredImage: "/path/image.jpg");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.FilePath.Should().Be("/path/to/_posts/2024/test-post.md");
        result.Data.IsDraft.Should().BeFalse();
        blogService.Received(1).AddPost("Test Post", false, true, false, "/path/image.jpg", null, null);
    }

    [Fact]
    public void AddPost_SplitsCommaSeparatedTags()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.AddPost("Test Post", true, false, false, null, Arg.Any<IReadOnlyList<string>>(), null)
            .Returns("/path/to/_drafts/test-post.md");

        // Act
        AddPostTool.AddPost(blogService, "Test Post", tags: "csharp, dotnet");

        // Assert
        blogService.Received(1).AddPost("Test Post", true, false, false, null,
            Arg.Is<IReadOnlyList<string>>(t => t.SequenceEqual(new[] { "csharp", "dotnet" })), null);
    }

    [Fact]
    public void AddPost_WhenTargetExists_ReturnsFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.AddPost("Test Post", true, false, false, null, null, null)
            .Returns((string?)null);

        // Act
        var result = AddPostTool.AddPost(blogService, "Test Post");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already exists");
        result.Data.Should().BeNull();
    }
}
