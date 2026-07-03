using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ListDraftsToolTests
{
    [Fact]
    public void ListDrafts_WhenNoDrafts_ReturnsSuccessWithEmptyList()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.ListDrafts().Returns(new List<string>().AsReadOnly());

        // Act
        var result = ListDraftsTool.ListDrafts(blogService);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Drafts.Should().BeEmpty();
        blogService.Received(1).ListDrafts();
    }

    [Fact]
    public void ListDrafts_WhenDraftsExist_ReturnsThem()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var drafts = new List<string> { "draft1.md", "draft2.md" }.AsReadOnly();
        blogService.ListDrafts().Returns(drafts);

        // Act
        var result = ListDraftsTool.ListDrafts(blogService);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Drafts.Should().Equal("draft1.md", "draft2.md");
        blogService.Received(1).ListDrafts();
    }
}
