using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.YamlParsing;
using BlogHelper9000.Imaging;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Options;
using BlogHelper9000.Core;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class AddFeaturedImageToolTests
{
    [Fact]
    public async Task AddFeaturedImage_WhenPostNotFound_ReturnsFailureEnvelope()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "nonexistent", "nature");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Could not find post 'nonexistent'.");
    }

    [Fact]
    public async Task AddFeaturedImage_WhenImageLoadFails_ReturnsFailureEnvelope()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData("---\ntitle: My Post\n---\n\nContent"));

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        unsplashClient.LoadImageAsync("nature").Returns((Stream?)null);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed to load image from Unsplash");
    }

    [Fact]
    public async Task AddFeaturedImage_WhenSuccessful_ReturnsSuccessEnvelope()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData("---\ntitle: My Post\n---\n\nContent"));

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        await using var imageStream = new MemoryStream();
        unsplashClient.LoadImageAsync("nature").Returns(imageStream);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.PostTitle.Should().Be("My Post");
        await imageProcessor.Received(1).Process(Arg.Any<MarkdownFile>(), imageStream, null);
    }

    [Fact]
    public async Task AddFeaturedImage_WhenQueryOmitted_DerivesQueryFromTitle()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_drafts/my-post.md", new MockFileData("---\ntitle: Dynamic Port Assignment\n---\n\nContent"));

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        await using var imageStream = new MemoryStream();
        unsplashClient.LoadImageAsync("Dynamic Port Assignment").Returns(imageStream);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md");

        // Assert
        result.Success.Should().BeTrue();
        await unsplashClient.Received(1).LoadImageAsync("Dynamic Port Assignment", Arg.Any<CancellationToken>());
    }
}
