using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.Handlers;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Handlers;

public class AddCommandHandlerTests
{
    private const string BaseDir = "/blog";

    [Fact]
    public void Should_Accept_PostTitle()
    {
        var console = new TestConsole();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = new AddCommandHandler(fileSystem, BaseDir, console);

        sut.Execute("Some shiny new blog post", Array.Empty<string>(), string.Empty, false, false, false);

        console.Out.ToString().Should().NotBeNull();
    }

    [Fact]
    public void Should_Add_NewPost_AsDraft()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var console = new TestConsole();
        var sut = new AddCommandHandler(fileSystem, BaseDir, console);

        sut.Execute("New post in draft", Array.Empty<string>(), string.Empty, false, false, true);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemBuilder.Drafts, "new-post-in-draft.md"))
            .Should().BeTrue();
    }

    [Fact]
    public void Should_Add_NewPost_StraightToPosts()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var console = new TestConsole();
        var sut = new AddCommandHandler(fileSystem, BaseDir, console);

        sut.Execute("New post in posts", Array.Empty<string>(), string.Empty, false, false, false);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemBuilder.Posts, "new-post-in-posts.md"))
            .Should().BeTrue();
    }
}