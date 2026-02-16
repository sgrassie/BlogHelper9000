using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class FixMetadataToolTests
{
    [Fact]
    public void FixMetadata_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();

        // Act
        var result = FixMetadataTool.FixMetadata(blogService, fixStatus: true, fixDescription: false, fixTags: true);

        // Assert
        result.Should().Be("Metadata fix completed successfully.");
        blogService.Received(1).FixMetadata(true, false, true);
    }

    [Fact]
    public void FixMetadata_WithAllFlagsTrue_CallsServiceCorrectly()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();

        // Act
        var result = FixMetadataTool.FixMetadata(blogService, fixStatus: true, fixDescription: true, fixTags: true);

        // Assert
        result.Should().Be("Metadata fix completed successfully.");
        blogService.Received(1).FixMetadata(true, true, true);
    }
}
