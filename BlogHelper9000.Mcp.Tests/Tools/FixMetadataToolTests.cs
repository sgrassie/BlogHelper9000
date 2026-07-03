using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class FixMetadataToolTests
{
    [Fact]
    public void FixMetadata_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.FixMetadata(true, false, true, false).Returns(new FixMetadataResult());

        // Act
        var result = FixMetadataTool.FixMetadata(blogService, fixStatus: true, fixDescription: false, fixTags: true, dryRun: false);

        // Assert
        result.Success.Should().BeTrue();
        blogService.Received(1).FixMetadata(true, false, true, false);
    }

    [Fact]
    public void FixMetadata_DefaultsToDryRun()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.FixMetadata(true, true, true, true).Returns(new FixMetadataResult());

        // Act
        var result = FixMetadataTool.FixMetadata(blogService, fixStatus: true, fixDescription: true, fixTags: true);

        // Assert
        result.Data!.DryRun.Should().BeTrue();
        blogService.Received(1).FixMetadata(true, true, true, true);
    }

    [Fact]
    public void FixMetadata_ReturnsUpdatedAndSkippedFiles()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        var coreResult = new FixMetadataResult();
        coreResult.Updated.Add("/blog/_posts/2024-01-01-good.md");
        coreResult.Skipped.Add(new FixMetadataSkip("/blog/_posts/bad.md", "Could not extract a date"));
        blogService.FixMetadata(true, false, false, false).Returns(coreResult);

        // Act
        var result = FixMetadataTool.FixMetadata(blogService, fixStatus: true, dryRun: false);

        // Assert
        result.Data!.Updated.Should().ContainSingle().Which.Should().Be("/blog/_posts/2024-01-01-good.md");
        result.Data.Skipped.Should().ContainSingle();
        result.Data.Skipped[0].FilePath.Should().Be("/blog/_posts/bad.md");
        result.Data.Skipped[0].Reason.Should().Be("Could not extract a date");
    }

    [Fact]
    public void FixMetadata_WhenServiceThrows_ReturnsFailureEnvelope()
    {
        // Arrange
        var blogService = Substitute.For<IBlogService>();
        blogService.When(x => x.FixMetadata(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()))
            .Do(_ => throw new InvalidOperationException("Test error"));

        // Act
        var result = FixMetadataTool.FixMetadata(blogService, fixStatus: true);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Test error");
    }
}
