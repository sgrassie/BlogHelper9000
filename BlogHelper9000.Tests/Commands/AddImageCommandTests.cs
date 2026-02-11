using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Imaging;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class AddImageCommandTests
{
    private IOptions<BlogHelperOptions> _options;

    public AddImageCommandTests()
    {
        _options = Options.Create(new BlogHelperOptions
        {
            BaseDirectory = "/blog"
        });
    }

    [Fact]
    public async Task Should_LogCorrectError_WhenPostCannotBeFound()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var logger = Substitute.For<MockLogger<AddImageCommand.Handler>>();
        var mockClient = Substitute.For<IUnsplashClient>();

        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem),
            Options.Create(new BlogHelperOptions { BaseDirectory = "/somefolder" }));
        var command = new AddImageCommand
        {
            Post = "file-does-not-exist.md"
        };

        var sut = new AddImageCommand.Handler(logger, postManager, mockClient, new ImageProcessor(logger, postManager));

        await sut.Handle(command, CancellationToken.None);

        logger.Received()
            .Log(LogLevel.Error, "Could not find file-does-not-exist.md to add an image to");
    }

    [Fact]
    public async Task Should_RequestImage_ForGivenQuery()
    {
        var fileData = """
                       ---
                       layout: post
                       title: test post
                       ---
                       """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2000-01-01-first-post.md", new MockFileData(fileData))
            .AddFile("/blog/assets/images/branding_logo.png", new MockFileData(string.Empty))
            .BuildFileSystem();
        var imageProcessor = Substitute.For<IImageProcessor>();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var mockClient = Substitute.For<IUnsplashClient>();

        var command = new AddImageCommand
        {
            Post = "2000-01-01-first-post.md",
            AuthorBranding = "branding_logo.png"
        };
        var sut = new AddImageCommand.Handler(NullLogger<AddImageCommand.Handler>.Instance, postManager, mockClient, imageProcessor);

        await sut.Handle(command, CancellationToken.None);

        await imageProcessor.Received(1).Process(Arg.Any<MarkdownFile>(), Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData("2000-01-01-first-post.md")]
    [InlineData("/_posts/2000-01-01-first-post.md")]
    [InlineData("/blog/_posts/2000-01-01-first-post.md")]
    public async Task Should_IgnoreFindThePost_(string postNameToFind)
    {
        var fileData = """
                       ---
                       layout: post
                       title: test post
                       ---
                       """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2000-01-01-first-post.md", new MockFileData(fileData))
            .AddFile("/blog/assets/images/branding_logo.png", new MockFileData(string.Empty))
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var imageProcessor = Substitute.For<IImageProcessor>();
        var mockClient = Substitute.For<IUnsplashClient>();
        var command = new AddImageCommand
        {
            Post = "2000-01-01-first-post.md",
            AuthorBranding = "branding_logo.png"
        };
        var sut = new AddImageCommand.Handler(NullLogger<AddImageCommand.Handler>.Instance, postManager, mockClient, imageProcessor);

        await sut.Handle(command, CancellationToken.None);

        await imageProcessor.Received(1).Process(Arg.Any<MarkdownFile>(), Arg.Any<Stream>(), Arg.Any<string>());
    }
}
