using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class UpdatePostToolTests
{
    private static PostManager CreatePostManager(MockFileSystem fileSystem) =>
        new(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));

    [Fact]
    public void UpdatePost_WhenPostNotFound_ReturnsFailureEnvelope()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "nonexistent.md", title: "New Title");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void UpdatePost_Title_Only_Leaves_Body_And_OtherFields_Untouched()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\ndescription: Keep me\n---\n\nOriginal body."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", title: "Updated Title");

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().Equal("title");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.Title.Should().Be("Updated Title");
        reloaded.Metadata.Description.Should().Be("Keep me");
        postManager.GetPostBody(result.Data.FilePath).Should().Be("Original body.");
    }

    [Fact]
    public void UpdatePost_Body_Only_Leaves_FrontMatter_Untouched()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nOld body."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", body: "New body.");

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().Equal("body");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.Title.Should().Be("Original");
        postManager.GetPostBody(result.Data.FilePath).Should().Be("New body.");
    }

    [Fact]
    public void UpdatePost_Tags_ReplacesExistingTags()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\ntags: [old]\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", tags: "csharp, dotnet");

        result.Data!.UpdatedFields.Should().Equal("tags");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.Tags.Should().Equal("csharp", "dotnet");
    }

    [Fact]
    public void UpdatePost_WithNoFieldsSupplied_MakesNoChanges()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md");

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().BeEmpty();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }
}
