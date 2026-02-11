using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class PublishCommandTests
{
    private readonly IOptions<BlogHelperOptions> _options = Options.Create(new BlogHelperOptions
    {
        BaseDirectory = "./blog"
    });

    [Fact]
    public async Task Should_OutputError_WhenPostToPublish_IsNotFound()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 01)));
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var logger = Substitute.For<MockLogger<PublishCommand.Handler>>();
        var command = new PublishCommand
        {
            Post = "file-does-not-exist.md"
        };
        var sut = new PublishCommand.Handler(logger, postManager, fakeTimeProvider);

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
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var command = new PublishCommand
        {
            Post = "a-test-post.md"
        };
        var sut = new PublishCommand.Handler(NullLogger<PublishCommand.Handler>.Instance, postManager, fakeTimeProvider);

        await sut.Handle(command, CancellationToken.None);
        var publishedPost = postManager.FileSystem.Directory
            .GetFiles("/blog/_posts/2024/").First(x => x.EndsWith("a-test-post.md"));
        var parsedPost = postManager.Markdown.LoadFile(publishedPost);

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
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var command = new PublishCommand
        {
            Post = "a-test-post.md"
        };
        var sut = new PublishCommand.Handler(NullLogger<PublishCommand.Handler>.Instance, postManager, fakeTimeProvider);

        await sut.Handle(command, CancellationToken.None);
        var publishedPost = postManager.FileSystem.Directory
            .GetFiles("/blog/_posts/2024/").First(x => x.EndsWith("a-test-post.md"));
        var parsedPost = postManager.Markdown.LoadFile(publishedPost);

        publishedPost.Should().EndWith($"{fakeTimeProvider.GetUtcNow().DateTime:yyyy-MM-dd}-a-test-post.md");
        parsedPost.Metadata.PublishedOn.Should().Be(fakeTimeProvider.GetUtcNow().DateTime.Date);
        parsedPost.Metadata.IsPublished.Should().BeTrue();
    }
}
