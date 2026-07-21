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

        unsplashClient.LoadImageAsync("nature").Returns((UnsplashImageResult?)null);

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
        var unsplashResult = new UnsplashImageResult(imageStream, "photo-1", "https://unsplash.com/photos/photo-1",
            "Jane Doe", "janedoe", "https://unsplash.com/@janedoe", "A description");
        unsplashClient.LoadImageAsync("nature").Returns(unsplashResult);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.PostTitle.Should().Be("My Post");
        result.Data.PhotoId.Should().Be("photo-1");
        result.Data.PhotographerName.Should().Be("Jane Doe");
        result.Data.PhotographerProfileUrl.Should().Be("https://unsplash.com/@janedoe");
        result.Data.PhotoUrl.Should().Be("https://unsplash.com/photos/photo-1");
        result.Data.Attribution.Should().Be("Photo by Jane Doe on Unsplash");
        await imageProcessor.Received(1).Process(Arg.Any<MarkdownFile>(), imageStream, null, "Photo by Jane Doe on Unsplash");
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
        var unsplashResult = new UnsplashImageResult(imageStream, "photo-1", "https://unsplash.com/photos/photo-1",
            "Jane Doe", "janedoe", "https://unsplash.com/@janedoe", "A description");
        unsplashClient.LoadImageAsync("Dynamic Port Assignment").Returns(unsplashResult);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md");

        // Assert
        result.Success.Should().BeTrue();
        await unsplashClient.Received(1).LoadImageAsync("Dynamic Port Assignment", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFeaturedImage_WhenPostAlreadyHasImage_AndReplaceFalse_ReturnsFailureEnvelope()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: My Post\nfeatured_image: /assets/images/my-post.webp\n---\n\nContent"));

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("replace=true");
        result.Error.Should().Contain("photoId");
        await unsplashClient.DidNotReceive().LoadImageAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFeaturedImage_WhenPostAlreadyHasImage_AndPhotoIdSupplied_ButReplaceFalse_ReturnsFailureEnvelope()
    {
        // Arrange — photoId alone must not bypass the consent guard.
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: My Post\nfeatured_image: /assets/images/my-post.webp\n---\n\nContent"));

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature", photoId: "photo-42");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("replace=true");
        await unsplashClient.DidNotReceive().LoadImageAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFeaturedImage_WhenPostAlreadyHasImage_AndReplaceTrue_Succeeds()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddFile("/blog/_drafts/my-post.md",
            new MockFileData("---\ntitle: My Post\nfeatured_image: /assets/images/my-post.webp\n---\n\nContent"));

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" });
        var markdownHandler = new MarkdownHandler(fileSystem);
        var postManager = new PostManager(fileSystem, markdownHandler, options);

        var unsplashClient = Substitute.For<IUnsplashClient>();
        var imageProcessor = Substitute.For<IImageProcessor>();

        await using var imageStream = new MemoryStream();
        var unsplashResult = new UnsplashImageResult(imageStream, "photo-1", "https://unsplash.com/photos/photo-1",
            "Jane Doe", "janedoe", "https://unsplash.com/@janedoe", "A description");
        unsplashClient.LoadImageAsync("nature").Returns(unsplashResult);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature", replace: true);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Attribution.Should().Be("Photo by Jane Doe on Unsplash");
        await imageProcessor.Received(1).Process(Arg.Any<MarkdownFile>(), imageStream, null, "Photo by Jane Doe on Unsplash");
    }

    [Fact]
    public async Task AddFeaturedImage_WhenPhotoIdSupplied_ForwardsToUnsplashClient()
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
        var unsplashResult = new UnsplashImageResult(imageStream, "photo-42", "https://unsplash.com/photos/photo-42",
            "Jane Doe", "janedoe", "https://unsplash.com/@janedoe", "A description");
        unsplashClient.LoadImageAsync("nature", "photo-42", Arg.Any<CancellationToken>()).Returns(unsplashResult);

        // Act
        var result = await AddFeaturedImageTool.AddFeaturedImage(
            postManager, unsplashClient, imageProcessor, "/blog/_drafts/my-post.md", "nature", photoId: "photo-42");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.PhotoId.Should().Be("photo-42");
        await unsplashClient.Received(1).LoadImageAsync("nature", "photo-42", Arg.Any<CancellationToken>());
    }
}
