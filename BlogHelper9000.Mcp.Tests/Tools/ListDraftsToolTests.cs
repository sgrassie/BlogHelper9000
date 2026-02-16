using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ListDraftsToolTests
{
    [Fact]
    public void ListDrafts_WhenNoDrafts_ReturnsNoDraftsMessage()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.ListDrafts().Returns(new List<string>().AsReadOnly());

        // Act
        var result = ListDraftsTool.ListDrafts(blogService);

        // Assert
        result.Should().Be("No drafts found.");
        blogService.Received(1).ListDrafts();
    }

    [Fact]
    public void ListDrafts_WhenDraftsExist_ReturnsJsonList()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var drafts = new List<string> { "draft1.md", "draft2.md" }.AsReadOnly();
        blogService.ListDrafts().Returns(drafts);

        // Act
        var result = ListDraftsTool.ListDrafts(blogService);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("draft1.md");
        result.Should().Contain("draft2.md");
        blogService.Received(1).ListDrafts();
    }
}
