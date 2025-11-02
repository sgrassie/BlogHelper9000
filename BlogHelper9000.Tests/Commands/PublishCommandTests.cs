using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Commands;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class PublishCommandTests 
{
    [Fact]
    public async Task Should_Output_Help()
    {
        // var console = new TestConsole();
        // var command = new PublishCommand();
        //
        // await command.InvokeAsync("publish -h", console);
        //
        // var lines = console.AsLines().ToList();
        //
        // lines.Should().Contain(x => x.StartsWith("Description:"));
        // lines.Should().Contain(x => x.StartsWith("Arguments:"));
        // lines.Should().Contain(x => x.StartsWith("<post> "));
    }
    
    [Fact]
    public async Task Should_OutputError_WhenPostToPublish_IsNotFound()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 01)));
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var logger = Substitute.For<MockLogger<PublishCommand.Handler>>();
        var command = new PublishCommand
        {
            BaseDirectory = "/blog",
            Post = "file-does-not-exist.md"
        };
        var sut = new PublishCommand.Handler(logger, fileSystem, fakeTimeProvider);
        
        await sut.Handle(command, CancellationToken.None);

        logger.Received()
            .Log(LogLevel.Error, "Could not find file-does-not-exist.md to publish");
    }

    [Fact]
    public async Task Should_Publish_DraftPost_AsFullyPublished()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 01)));
        const string header = """
                              ---
                              layout: post
                              ---
                              """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-test-post.md", new MockFileData(header))
            .BuildFileSystem();
        var fileSystemHelper = new PostManager(fileSystem, "/blog");
        
        var command = new PublishCommand
        {
            BaseDirectory = "/blog",
            Post = "a-test-post.md"
        };
        var sut = new PublishCommand.Handler(NullLogger<PublishCommand.Handler>.Instance, fileSystem, fakeTimeProvider);
        
        await sut.Handle(command, CancellationToken.None);
        var publishedPost = fileSystemHelper.FileSystem.Directory
            .GetFiles("/blog/_posts/2024/").First(x => x.EndsWith("a-test-post.md"));
        var parsedPost = fileSystemHelper.Markdown.LoadFile(publishedPost);

        publishedPost.Should().EndWith($"{fakeTimeProvider.GetUtcNow().DateTime:yyyy-MM-dd}-a-test-post.md");
        parsedPost.Metadata.PublishedOn.Should().Be(fakeTimeProvider.GetUtcNow().DateTime.Date);
        parsedPost.Metadata.IsPublished.Should().BeTrue();
    }
    
    [Fact]
    public async Task Should_Publish_DraftPost_InPostsFolder_AsFullyPublished()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 01)));
        const string header = """
                              ---
                              layout: post
                              ispublished: false
                              ---
                              """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/a-test-post.md", new MockFileData(header))
            .BuildFileSystem();
        var fileSystemHelper = new PostManager(fileSystem, "/blog");
        
        var command = new PublishCommand
        {
            BaseDirectory = "/blog",
            Post = "a-test-post.md"
        };
        var sut = new PublishCommand.Handler(NullLogger<PublishCommand.Handler>.Instance, fileSystem, fakeTimeProvider);
        
        await sut.Handle(command, CancellationToken.None);
        var publishedPost = fileSystemHelper.FileSystem.Directory
            .GetFiles("/blog/_posts/2024/").First(x => x.EndsWith("a-test-post.md"));
        var parsedPost = fileSystemHelper.Markdown.LoadFile(publishedPost);

        publishedPost.Should().EndWith($"{fakeTimeProvider.GetUtcNow().DateTime:yyyy-MM-dd}-a-test-post.md");
        parsedPost.Metadata.PublishedOn.Should().Be(fakeTimeProvider.GetUtcNow().DateTime.Date);
        parsedPost.Metadata.IsPublished.Should().BeTrue();
    }
}