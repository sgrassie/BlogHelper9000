using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class PatchPostToolTests
{
    private static PostManager CreatePostManager(MockFileSystem fileSystem) =>
        new(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));

    [Fact]
    public void PatchPost_WhenPostNotFound_ReturnsFailureEnvelope()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "nonexistent.md", "find", "replace");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("nonexistent.md");
    }

    [Fact]
    public void PatchPost_UniqueMatch_ReplacesTextAndLeavesFrontMatterUntouched()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\ndescription: Keep me\n---\n\nThe quick brown fox jumps.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "my-post.md", "brown fox", "lazy dog");

        result.Success.Should().BeTrue();
        result.Data!.Applied.Should().BeTrue();
        fileSystem.File.ReadAllText(result.Data.FilePath).Should().StartWith("---\n");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.Title.Should().Be("Original");
        reloaded.Metadata.Description.Should().Be("Keep me");
        postManager.GetPostBody(result.Data.FilePath).Should().Be("The quick lazy dog jumps.");
    }

    [Fact]
    public void PatchPost_ZeroOccurrences_FailsAndLeavesBodyUnchanged()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody text.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "my-post.md", "missing text", "replacement");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("get_post");
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void PatchPost_MultipleOccurrences_FailsWithCountAndLeavesBodyUnchanged()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nrepeat repeat repeat";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "my-post.md", "repeat", "once");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("3");
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void PatchPost_FindEqualsReplace_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nSame text here.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "my-post.md", "Same text", "Same text");

        result.Success.Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void PatchPost_EmptyFind_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody text.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "my-post.md", "", "replacement");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("find");
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void PatchPost_NestedPublishedPost_ReplacesText()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_posts/2024/2024-01-01-my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nOld content here."));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "2024-01-01-my-post.md", "Old content", "New content");

        result.Success.Should().BeTrue();
        postManager.GetPostBody(result.Data!.FilePath).Should().Be("New content here.");
    }

    [Fact]
    public void PatchPost_EmptyReplace_DeletesMatchedText()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nRemove this: text remains."));
        var postManager = CreatePostManager(fileSystem);

        var result = PatchPostTool.PatchPost(postManager, "my-post.md", "Remove this: ", "");

        result.Success.Should().BeTrue();
        postManager.GetPostBody(result.Data!.FilePath).Should().Be("text remains.");
    }
}
