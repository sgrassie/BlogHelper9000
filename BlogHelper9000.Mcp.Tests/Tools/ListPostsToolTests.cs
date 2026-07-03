using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ListPostsToolTests
{
    private static PostManager CreatePostManager(MockFileSystem fileSystem) =>
        new(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));

    [Fact]
    public void ListPosts_ReturnsPublishedPosts_NewestFirst()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_posts/2024-01-01-old.md",
            new MockFileData("---\ntitle: Old Post\npublished: 01/01/2024\nispublished: true\ntags: [a]\n---"));
        fileSystem.AddFile("/blog/_posts/2024-06-01-new.md",
            new MockFileData("---\ntitle: New Post\npublished: 01/06/2024\nispublished: true\ntags: [b]\n---"));
        var postManager = CreatePostManager(fileSystem);

        var result = ListPostsTool.ListPosts(postManager);

        result.Success.Should().BeTrue();
        result.Data!.Posts.Should().HaveCount(2);
        result.Data.Posts[0].Title.Should().Be("New Post");
        result.Data.Posts[1].Title.Should().Be("Old Post");
    }

    [Fact]
    public void ListPosts_ExcludesDrafts()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/draft.md", new MockFileData("---\ntitle: A Draft\n---"));
        fileSystem.AddFile("/blog/_posts/2024-01-01-published.md",
            new MockFileData("---\ntitle: Published\nispublished: true\n---"));
        var postManager = CreatePostManager(fileSystem);

        var result = ListPostsTool.ListPosts(postManager);

        result.Data!.Posts.Should().ContainSingle(p => p.Title == "Published");
    }

    [Fact]
    public void ListPosts_Respects_Limit()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        for (var i = 1; i <= 5; i++)
        {
            fileSystem.AddFile($"/blog/_posts/2024-01-0{i}-post{i}.md",
                new MockFileData($"---\ntitle: Post {i}\nispublished: true\n---"));
        }
        var postManager = CreatePostManager(fileSystem);

        var result = ListPostsTool.ListPosts(postManager, limit: 2);

        result.Data!.Posts.Should().HaveCount(2);
    }
}
