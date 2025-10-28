using BlogHelper9000.Commands;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogHelper9000.Tests.Handlers;

public class AddCommandHandlerTests
{
    [Fact]
    public void Should_Accept_PostTitle()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = new AddCommandHandler(NullLogger.Instance, new PostManager(fileSystem, "/blog"));
        var options = new AddOptions("Some shiny new blog post", Array.Empty<string>(), string.Empty, false, false, false);

        sut.Execute(options);

    }

    [Fact]
    public void Should_Add_NewPost_AsDraft()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = new AddCommandHandler(NullLogger.Instance, new PostManager(fileSystem, "/blog"));
        var options = new AddOptions("New post in draft", Array.Empty<string>(), string.Empty, true, false, true);

        sut.Execute(options);

        fileSystem
            .File
            .Exists(fileSystem.Path.Combine(JekyllBlogFilesystemBuilder.Drafts, "new-post-in-draft.md"))
            .Should().BeTrue();
    }

    [Fact]
    public void Should_Add_NewPost_StraightToPosts()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = new AddCommandHandler(NullLogger.Instance, new PostManager(fileSystem, "/blog"));
        var options = new AddOptions("New post in posts", Array.Empty<string>(), string.Empty, false, false, false);

        sut.Execute(options);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemBuilder.Posts, "new-post-in-posts.md"))
            .Should().BeTrue();
    }
}