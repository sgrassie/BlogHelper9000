using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class GetPostToolTests
{
    private static PostManager CreatePostManager(MockFileSystem fileSystem, string baseDirectory = "/blog")
    {
        var options = Options.Create(new BlogHelperOptions { BaseDirectory = baseDirectory });
        return new PostManager(fileSystem, new MarkdownHandler(fileSystem), options);
    }

    [Fact]
    public void GetPost_WhenPostNotFound_ReturnsFailureEnvelope()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");
        var postManager = CreatePostManager(fileSystem);

        var result = GetPostTool.GetPost(postManager, "nonexistent.md");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("nonexistent.md");
    }

    [Fact]
    public void GetPost_ReturnsFrontMatterAndBody_ForDraft()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: My Post\ndescription: A test\ntags: [csharp,dotnet]\n---\n\nHello, world."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetPostTool.GetPost(postManager, "my-post.md");

        result.Success.Should().BeTrue();
        result.Data!.IsDraft.Should().BeTrue();
        result.Data.Body.Should().Be("Hello, world.");
        result.Data.FrontMatter["title"].Should().Be("My Post");
        result.Data.FrontMatter["description"].Should().Be("A test");
    }

    [Fact]
    public void GetPost_IncludesExtras_InFrontMatter()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: My Post\npermalink: /foo/\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetPostTool.GetPost(postManager, "my-post.md");

        result.Data!.FrontMatter["permalink"].Should().Be("/foo/");
    }

    [Fact]
    public void GetPost_IsDraft_False_ForPublishedPost()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_posts/2024-01-01-my-post.md",
            new MockFileData("---\ntitle: My Post\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetPostTool.GetPost(postManager, "2024-01-01-my-post.md");

        result.Data!.IsDraft.Should().BeFalse();
    }
}
