using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Core.YamlParsing;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class GetBlogInfoToolTests
{
    [Fact]
    public void GetBlogInfo_ReturnsBlogStatistics()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var blogInfo = new BlogMetaInformation
        {
            PostCount = 10,
            UnPublishedCount = 2,
            DaysSinceLastPost = TimeSpan.FromDays(5),
            LatestPosts = new()
            {
                new YamlHeader { Title = "Latest Post", PublishedOn = DateTime.Parse("2024-01-01"), Tags = new List<string> { "tag1" } }
            },
            Unpublished = new[]
            {
                new YamlHeader { Title = "Draft", Extras = new Dictionary<string, string> { { "originalFilename", "draft.md" } } }
            }
        };
        blogService.GetBlogInfo().Returns(blogInfo);

        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });

        // Act
        var result = GetBlogInfoTool.GetBlogInfo(blogService, options, fileSystem);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.BaseDirectory.Should().Be("/blog");
        result.Data.LooksLikeJekyllBlog.Should().BeTrue();
        result.Data.PostCount.Should().Be(10);
        result.Data.UnPublishedCount.Should().Be(2);
        result.Data.DaysSinceLastPost.Should().Be(5);
        result.Data.LatestPosts.Should().ContainSingle(p => p.Title == "Latest Post");
        result.Data.Unpublished.Should().ContainSingle(p => p.OriginalFilename == "draft.md");
        blogService.Received(1).GetBlogInfo();
    }

    [Fact]
    public void GetBlogInfo_ReportsFalse_WhenBaseDirectory_HasNoPostsOrDrafts()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.GetBlogInfo().Returns(new BlogMetaInformation());

        var fileSystem = new MockFileSystem();
        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/not-a-blog" });

        // Act
        var result = GetBlogInfoTool.GetBlogInfo(blogService, options, fileSystem);

        // Assert
        result.Data!.LooksLikeJekyllBlog.Should().BeFalse();
    }

    [Fact]
    public void GetBlogInfo_ReturnsNull_ForDaysSinceLastPost_WhenNoPublishedPosts()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.GetBlogInfo().Returns(new BlogMetaInformation { DaysSinceLastPost = null });

        var fileSystem = new MockFileSystem();
        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });

        // Act
        var result = GetBlogInfoTool.GetBlogInfo(blogService, options, fileSystem);

        // Assert
        result.Data!.DaysSinceLastPost.Should().BeNull();
    }
}
