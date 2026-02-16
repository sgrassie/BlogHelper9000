using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Core.YamlParsing;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;
using System.Text.Json;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class GetBlogInfoToolTests
{
    [Fact]
    public void GetBlogInfo_ReturnsJsonWithBlogStatistics()
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

        // Act
        var result = GetBlogInfoTool.GetBlogInfo(blogService);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("PostCount").GetInt32().Should().Be(10);
        json.RootElement.GetProperty("UnPublishedCount").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("DaysSinceLastPost").GetInt32().Should().Be(5);
        blogService.Received(1).GetBlogInfo();
    }
}
