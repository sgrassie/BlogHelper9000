using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class AppendToPostToolTests
{
    private static PostManager CreatePostManager(MockFileSystem fileSystem) =>
        new(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));

    [Fact]
    public void AppendToPost_WhenPostNotFound_ReturnsFailureEnvelope()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        var postManager = CreatePostManager(fileSystem);

        var result = AppendToPostTool.AppendToPost(postManager, "nonexistent.md", "Some content.");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("nonexistent.md");
    }

    [Fact]
    public void AppendToPost_ToNonEmptyBody_AddsBlankLineSeparator()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nFirst paragraph."));
        var postManager = CreatePostManager(fileSystem);

        var result = AppendToPostTool.AppendToPost(postManager, "my-post.md", "Second paragraph.");

        result.Success.Should().BeTrue();
        postManager.GetPostBody(result.Data!.FilePath).Should().Be("First paragraph.\n\nSecond paragraph.");
    }

    [Fact]
    public void AppendToPost_ToEmptyBody_SetsBodyToContent()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\n---\n"));
        var postManager = CreatePostManager(fileSystem);

        var result = AppendToPostTool.AppendToPost(postManager, "my-post.md", "First content.");

        result.Success.Should().BeTrue();
        postManager.GetPostBody(result.Data!.FilePath).Should().Be("First content.");
    }

    [Fact]
    public void AppendToPost_EmptyContent_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = AppendToPostTool.AppendToPost(postManager, "my-post.md", "   ");

        result.Success.Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void AppendToPost_ToPublishedPost_AppendsContent()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_posts/2024/2024-01-01-my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nExisting body."));
        var postManager = CreatePostManager(fileSystem);

        var result = AppendToPostTool.AppendToPost(postManager, "2024-01-01-my-post.md", "New paragraph.");

        result.Success.Should().BeTrue();
        postManager.GetPostBody(result.Data!.FilePath).Should().Be("Existing body.\n\nNew paragraph.");
    }
}
