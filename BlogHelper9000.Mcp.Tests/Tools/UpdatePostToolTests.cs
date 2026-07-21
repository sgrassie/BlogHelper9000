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

    [Fact]
    public void UpdatePost_FeaturedAndHidden_SetsFlagsAndReportsFields()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", featured: true, hidden: true);

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().Equal("featured", "hidden");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.IsFeatured.Should().BeTrue();
        reloaded.Metadata.IsHidden.Should().BeTrue();
    }

    [Fact]
    public void UpdatePost_PublishedOn_Valid_UpdatesDateWithoutChangingPublishStatus()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_posts/2024/2024-01-01-my-post.md",
            new MockFileData("---\ntitle: Original\npublished: 01/01/2024\nispublished: true\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "2024-01-01-my-post.md", publishedOn: "2024-02-15");

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().Equal("publishedOn");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.PublishedOn.Should().Be(new DateTime(2024, 2, 15));
        reloaded.Metadata.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void UpdatePost_PublishedOn_Invalid_FailsWithoutChangingFile()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", publishedOn: "not-a-date");

        result.Success.Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void UpdatePost_PublishedOn_SlashFormat_FailsRegardlessOfCurrentCulture()
    {
        // DateOnly.TryParse is culture-sensitive (e.g. "03/04/2024" is 3 Apr under en-GB but
        // 4 Mar under en-US) — publishedOn must be parsed strictly as invariant yyyy-MM-dd so
        // the same input always means the same date no matter which locale the server runs in.
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\npublished: 01/01/2024\nispublished: true\n---\n\nBody.";
        fileSystem.AddFile("/blog/_posts/2024/2024-01-01-my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "2024-01-01-my-post.md", publishedOn: "03/04/2024");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not a valid yyyy-MM-dd date");
        fileSystem.File.ReadAllText("/blog/_posts/2024/2024-01-01-my-post.md").Should().Be(original);
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_AddsNewKey()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md",
            extraFrontMatter: "canonical_url: https://example.com/post");

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().Equal("extra:canonical_url");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.Extras.Should().ContainKey("canonical_url")
            .WhoseValue.Should().Be("https://example.com/post");
        fileSystem.File.ReadAllText(result.Data.FilePath).Should().Contain("canonical_url: https://example.com/post");
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_UpdatesExistingKey()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\ncanonical_url: https://old.example.com\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md",
            extraFrontMatter: "canonical_url: https://new.example.com");

        result.Success.Should().BeTrue();
        var reloaded = postManager.Markdown.LoadFile(result.Data!.FilePath);
        reloaded.Metadata.Extras["canonical_url"].Should().Be("https://new.example.com");
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_EmptyValueRemovesKey()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: Original\ncanonical_url: https://old.example.com\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", extraFrontMatter: "canonical_url:");

        result.Success.Should().BeTrue();
        result.Data!.UpdatedFields.Should().Equal("extra:canonical_url");
        var reloaded = postManager.Markdown.LoadFile(result.Data.FilePath);
        reloaded.Metadata.Extras.Should().NotContainKey("canonical_url");
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_CollidesWithKnownKey_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", extraFrontMatter: "featured: true");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("featured");
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_MalformedLine_NoColon_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", extraFrontMatter: "not-a-key-value-pair");

        result.Success.Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_MalformedLine_EmptyKey_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", extraFrontMatter: ": value with no key");

        result.Success.Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }

    [Fact]
    public void UpdatePost_ExtraFrontMatter_ReservedKey_Fails()
    {
        var fileSystem = new MockFileSystem();
        const string original = "---\ntitle: Original\n---\n\nBody.";
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData(original));
        var postManager = CreatePostManager(fileSystem);

        var result = UpdatePostTool.UpdatePost(postManager, "my-post.md", extraFrontMatter: "originalFilename: something.md");

        result.Success.Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_drafts/my-post.md").Should().Be(original);
    }
}
