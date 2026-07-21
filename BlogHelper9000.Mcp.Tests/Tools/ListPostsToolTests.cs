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

    [Fact]
    public void ListPosts_ReportsHasFeaturedImage_PerPost()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_posts/2024-01-01-with-image.md",
            new MockFileData("---\ntitle: With Image\npublished: 01/01/2024\nispublished: true\nfeatured_image: /assets/images/with-image.webp\n---"));
        fileSystem.AddFile("/blog/_posts/2024-01-02-without-image.md",
            new MockFileData("---\ntitle: Without Image\npublished: 01/02/2024\nispublished: true\n---"));
        var postManager = CreatePostManager(fileSystem);

        var result = ListPostsTool.ListPosts(postManager);

        result.Data!.Posts.Should().ContainSingle(p => p.Title == "With Image" && p.HasFeaturedImage);
        result.Data.Posts.Should().ContainSingle(p => p.Title == "Without Image" && !p.HasFeaturedImage);
    }

    [Fact]
    public void ListPosts_WhenMissingFeaturedImageTrue_ReturnsOnlyImagelessPublishedPosts()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_posts/2024-01-01-with-image.md",
            new MockFileData("---\ntitle: With Image\npublished: 01/01/2024\nispublished: true\nfeatured_image: /assets/images/with-image.webp\n---"));
        fileSystem.AddFile("/blog/_posts/2024-01-02-without-image.md",
            new MockFileData("---\ntitle: Without Image\npublished: 01/02/2024\nispublished: true\n---"));
        var postManager = CreatePostManager(fileSystem);

        var result = ListPostsTool.ListPosts(postManager, missingFeaturedImage: true);

        result.Data!.Posts.Should().ContainSingle();
        result.Data.Posts[0].Title.Should().Be("Without Image");
    }

    [Fact]
    public void ListPosts_MissingFeaturedImage_AppliesBeforeLimit()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        // Newest post has an image; the two older posts don't.
        fileSystem.AddFile("/blog/_posts/2024-01-01-without-image-a.md",
            new MockFileData("---\ntitle: Without Image A\npublished: 01/01/2024\nispublished: true\n---"));
        fileSystem.AddFile("/blog/_posts/2024-01-02-without-image-b.md",
            new MockFileData("---\ntitle: Without Image B\npublished: 01/02/2024\nispublished: true\n---"));
        fileSystem.AddFile("/blog/_posts/2024-01-03-with-image.md",
            new MockFileData("---\ntitle: With Image\npublished: 01/03/2024\nispublished: true\nfeatured_image: /assets/images/with-image.webp\n---"));
        var postManager = CreatePostManager(fileSystem);

        // If the limit were applied before the filter, Take(1) would grab the newest post (which has an
        // image), and filtering it out afterwards would leave zero results. Filtering first must instead
        // return the newest of the two image-less posts.
        var result = ListPostsTool.ListPosts(postManager, limit: 1, missingFeaturedImage: true);

        result.Data!.Posts.Should().ContainSingle();
        result.Data.Posts[0].Title.Should().Be("Without Image B");
        result.Data.Posts[0].HasFeaturedImage.Should().BeFalse();
    }
}
