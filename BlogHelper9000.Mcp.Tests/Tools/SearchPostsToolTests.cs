using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class SearchPostsToolTests
{
    private static PostSearchService CreateSearchService(MockFileSystem fileSystem)
    {
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));
        return new PostSearchService(postManager);
    }

    [Fact]
    public void SearchPosts_WhenQueryAndTagBothEmpty_ReturnsFailureEnvelope()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");
        var searchService = CreateSearchService(fileSystem);

        var result = SearchPostsTool.SearchPosts(searchService, query: "  ", tag: null);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Provide a query and/or a tag to search for.");
    }

    [Fact]
    public void SearchPosts_WhenMatchFound_ReturnsMatchInStructuredResult()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/my-draft.md",
            new MockFileData("---\ntitle: Kubernetes Basics\ntags: [kubernetes]\n---\n\nAll about kubernetes networking."));
        var searchService = CreateSearchService(fileSystem);

        var result = SearchPostsTool.SearchPosts(searchService, query: "kubernetes");

        result.Success.Should().BeTrue();
        result.Data!.Matches.Should().ContainSingle(m => m.FileName == "my-draft.md");
        result.Data.Matches.Single().Title.Should().Be("Kubernetes Basics");
        result.Data.Matches.Single().IsDraft.Should().BeTrue();
        result.Data.TotalMatches.Should().Be(1);
    }

    [Fact]
    public void SearchPosts_WhenNoMatches_ReturnsSuccessWithEmptyList()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: Unrelated\n---\n\nNothing here."));
        var searchService = CreateSearchService(fileSystem);

        var result = SearchPostsTool.SearchPosts(searchService, query: "kubernetes");

        result.Success.Should().BeTrue();
        result.Data!.Matches.Should().BeEmpty();
        result.Data.TotalMatches.Should().Be(0);
    }

    [Fact]
    public void SearchPosts_TagOnly_Search_Succeeds_With_Empty_Query()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/my-draft.md",
            new MockFileData("---\ntitle: A Draft\ntags: ['Csharp']\n---\n\nBody."));
        var searchService = CreateSearchService(fileSystem);

        var result = SearchPostsTool.SearchPosts(searchService, query: string.Empty, tag: "csharp");

        result.Success.Should().BeTrue();
        result.Data!.Matches.Should().ContainSingle(m => m.FileName == "my-draft.md");
    }
}
