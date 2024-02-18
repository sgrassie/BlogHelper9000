using System.CommandLine.IO;
using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Handlers;

public class PublishCommandHandlerTests
{
    [Fact]
    public void Should_OutputError_WhenPostToPublish_IsNotFound()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var console = new TestConsole();
        var sut = new PublishCommandHandler(fileSystem, "/blog", console);
        
        sut.Execute("file-does-not-exist.md");

        var lines = console.AsLines();

        lines.Should().Contain(x => x == "Could not find specified post to publish");
    }

    [Fact]
    public void Should_Publish_DraftPost_AsFullyPublished()
    {
        const string header = """
                              ---
                              layout: post
                              ---
                              """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-test-post.md", new MockFileData(header))
            .BuildFileSystem();
        var fileSystemHelper = new PostManager(fileSystem, "/blog");
        var console = new TestConsole();
        var sut = new PublishCommandHandler(fileSystem, "/blog", console);
        
        sut.Execute("a-test-post.md");
        var publishedPost = fileSystemHelper.FileSystem.Directory
            .GetFiles("/blog/_posts/2024/").First(x => x.EndsWith("a-test-post.md"));
        var parsedPost = fileSystemHelper.Markdown.LoadFile(publishedPost);

        publishedPost.Should().EndWith($"{DateTime.Now:yyyy-MM-dd}-a-test-post.md");
        parsedPost.Metadata.PublishedOn.Should().Be(DateTime.Now.Date);
        parsedPost.Metadata.IsPublished.Should().BeTrue();
    }
    
    [Fact]
    public void Should_Publish_DraftPost_InPostsFolder_AsFullyPublished()
    {
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
        var console = new TestConsole();
        var sut = new PublishCommandHandler(fileSystem, "/blog", console);
        
        sut.Execute("a-test-post.md");
        var publishedPost = fileSystemHelper.FileSystem.Directory
            .GetFiles("/blog/_posts/2024/").First(x => x.EndsWith("a-test-post.md"));
        var parsedPost = fileSystemHelper.Markdown.LoadFile(publishedPost);

        publishedPost.Should().EndWith($"{DateTime.Now:yyyy-MM-dd}-a-test-post.md");
        parsedPost.Metadata.PublishedOn.Should().Be(DateTime.Now.Date);
        parsedPost.Metadata.IsPublished.Should().BeTrue();
    }
}