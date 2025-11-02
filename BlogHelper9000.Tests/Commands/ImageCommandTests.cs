using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Commands;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Imager;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class ImageCommandTests
{
    [Fact]
    public async Task Should_Output_Help_ForMainImageCommand()
    {
        var expectedHelp = new List<string>
        {
            "Description:",
            "Manipulate images in blog posts",
            "Usage:",
            "image [command] [options]",
            "Options:",
            "-b, --base-directory <base-directory>  The base directory of the blog.",
            "--version                              Show version information",
            "-?, -h, --help                         Show help and usage information",
            "Commands:",
            "add <post> <query>  Add an image to a post [default: programming]",
            "update              Update images in posts"
        };
        // var console = new TestConsole();
        // var command = new ImageCommand();
        // command.AddOption(GlobalOptions.BaseDirectoryOption);
        //
        // await command.InvokeAsync("image -h", console);
        //
        // var lines = console.AsLines();
        //
        // lines.Should().ContainInOrder(expectedHelp);
    }

    [Fact(Skip = "Not including the commands for this just now")]
    public async Task Should_Output_Help_ForImageUpdateSubCommand()
    {
        var expectedHelp = new[]
        {
            "Description:", 
            "Update images in posts", 
            "Usage:", 
            "image update [command] [options]", 
            "Options:", 
            "-b, --branding <branding>  Optionally provide author branding. [default: /assets/images/branding_logo.png]",
            "-?, -h, --help             Show help and usage information", 
            "Commands:", 
            "post <post> <query>  Updates the image in a specific post [default: programming]",
            "all <query>          Updates all images in all posts [default: programming]"
        };
        // var console = new TestConsole();
        // var command = new ImageCommand();
        // command.AddOption(GlobalOptions.BaseDirectoryOption);
        //
        // await command.InvokeAsync("image update -h", console);
        //
        // var lines = console.AsLines();
        //
        // lines.Should().ContainInOrder(expectedHelp);
    }
    
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