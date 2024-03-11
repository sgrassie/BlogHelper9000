using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Imager;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BlogHelper9000.Tests.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public class ImageCommand
{
    public class AddSubCommandHandlerTests()
    {
        [Fact]
        public async Task Should_LogCorrectError_WhenPostCannotBeFound()
        {
            var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
            var logger = Substitute.For<MockLogger<ImageCommandAddSubCommandHandler>>();
            var postManager = new PostManager(fileSystem, "/blog");
            var mockClient = Substitute.For<IUnsplashClient>();
            var sut = new ImageCommandAddSubCommandHandler(logger, postManager, mockClient, new ImageProcessor(logger, postManager));

            await sut.Execute("file-does-not-exist.md", "programming", "/somepath");

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
                .BuildFileSystem();
            var logger = Substitute.For<MockLogger<ImageCommandAddSubCommandHandler>>();
            var imageProcessor = Substitute.For<IImageProcessor>();
            var postManager = new PostManager(fileSystem, "/blog");
            var mockClient = Substitute.For<IUnsplashClient>();
            var sut = new ImageCommandAddSubCommandHandler(logger, postManager, mockClient, imageProcessor);

            await sut.Execute("2000-01-01-first-post.md", "unit tests", "test");

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
                .BuildFileSystem();
            var logger = Substitute.For<MockLogger<ImageCommandAddSubCommandHandler>>();
            var imageProcessor = Substitute.For<IImageProcessor>();
            var postManager = new PostManager(fileSystem, "/blog");
            var mockClient = Substitute.For<IUnsplashClient>();
            var sut = new ImageCommandAddSubCommandHandler(logger, postManager, mockClient, imageProcessor);

            await sut.Execute(postNameToFind, "unit tests", "test");

            await imageProcessor.Received(1).Process(Arg.Any<MarkdownFile>(), Arg.Any<Stream>(), Arg.Any<string>());
        }
    }
}
