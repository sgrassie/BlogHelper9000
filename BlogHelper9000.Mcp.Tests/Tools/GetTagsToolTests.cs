using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class GetTagsToolTests
{
    private static PostManager CreatePostManager(MockFileSystem fileSystem) =>
        new(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));

    [Fact]
    public void GetTags_SplitsCountsBetweenPublishedAndDrafts()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/draft-one.md",
            new MockFileData("---\ntitle: Draft One\ntags: [dotnet]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_posts/2024/2024-01-01-post-one.md",
            new MockFileData("---\ntitle: Post One\npublished: 01/01/2024\nispublished: true\ntags: [dotnet]\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetTagsTool.GetTags(postManager);

        result.Success.Should().BeTrue();
        var dotnet = result.Data!.Tags.Should().ContainSingle(t => t.Tag == "dotnet").Subject;
        dotnet.Total.Should().Be(2);
        dotnet.Published.Should().Be(1);
        dotnet.Drafts.Should().Be(1);
    }

    [Fact]
    public void GetTags_CaseInsensitiveGrouping_PicksMostFrequentCasingAsCanonical()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/draft-one.md",
            new MockFileData("---\ntitle: Draft One\ntags: [csharp]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/draft-two.md",
            new MockFileData("---\ntitle: Draft Two\ntags: [csharp]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/draft-three.md",
            new MockFileData("---\ntitle: Draft Three\ntags: [Csharp]\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetTagsTool.GetTags(postManager);

        var tag = result.Data!.Tags.Should().ContainSingle(t => string.Equals(t.Tag, "csharp", StringComparison.OrdinalIgnoreCase)).Subject;
        tag.Tag.Should().Be("csharp");
        tag.Total.Should().Be(3);
        tag.Variants.Should().Equal("Csharp");
    }

    [Fact]
    public void GetTags_QuotedTagsGroupWithUnquotedTag()
    {
        var fileSystem = new MockFileSystem();
        // YAML flow items: ["'Csharp'"] -> tag value is literally 'Csharp' (single quotes as content);
        // ['"csharp"'] -> tag value is literally "csharp" (double quotes as content).
        fileSystem.AddFile("/blog/_drafts/draft-one.md",
            new MockFileData("---\ntitle: Draft One\ntags: [\"'Csharp'\"]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/draft-two.md",
            new MockFileData("---\ntitle: Draft Two\ntags: ['\"csharp\"']\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/draft-three.md",
            new MockFileData("---\ntitle: Draft Three\ntags: [csharp]\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetTagsTool.GetTags(postManager);

        result.Data!.Tags.Should().ContainSingle(t => string.Equals(t.Tag, "csharp", StringComparison.OrdinalIgnoreCase));
        var tag = result.Data.Tags.Single(t => string.Equals(t.Tag, "csharp", StringComparison.OrdinalIgnoreCase));
        tag.Total.Should().Be(3);
    }

    [Fact]
    public void GetTags_SortsByTotalDescendingThenTagAscending()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/draft-one.md",
            new MockFileData("---\ntitle: Draft One\ntags: [zeta]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/draft-two.md",
            new MockFileData("---\ntitle: Draft Two\ntags: [alpha]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/draft-three.md",
            new MockFileData("---\ntitle: Draft Three\ntags: [beta, alpha]\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetTagsTool.GetTags(postManager);

        result.Data!.Tags.Select(t => t.Tag).Should().Equal("alpha", "beta", "zeta");
    }

    [Fact]
    public void GetTags_EmptyBlog_ReturnsEmptyTagsWithPopulatedRules()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");
        var postManager = CreatePostManager(fileSystem);

        var result = GetTagsTool.GetTags(postManager);

        result.Success.Should().BeTrue();
        result.Data!.Tags.Should().BeEmpty();
        result.Data.NormalisationRules.Should().NotBeEmpty();
    }

    [Fact]
    public void GetTags_DuplicateTagWithinOnePost_CountsOnce()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/draft-one.md",
            new MockFileData("---\ntitle: Draft One\ntags: [dotnet, dotnet]\n---\n\nBody."));
        var postManager = CreatePostManager(fileSystem);

        var result = GetTagsTool.GetTags(postManager);

        var tag = result.Data!.Tags.Single(t => t.Tag == "dotnet");
        tag.Total.Should().Be(1);
    }
}
