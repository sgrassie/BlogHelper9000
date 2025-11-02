using BlogHelper9000.Commands;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogHelper9000.Tests.Handlers;

public class AddCommandTests
{
    [Fact]
    public async Task Should_Add_NewPost_AsDraft()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var command = new AddCommand
        {
            BaseDirectory = "/blog",
            Title = "New post in draft",
            IsDraft = true,
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, fileSystem);
        
        await sut.Handle(command, CancellationToken.None);

        fileSystem
            .File
            .Exists(fileSystem.Path.Combine(JekyllBlogFilesystemBuilder.Drafts, "new-post-in-draft.md"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_Add_NewPost_StraightToPosts()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var command = new AddCommand
        {
            BaseDirectory = "/blog",
            Title = "New post in posts"
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, fileSystem);
        
        await sut.Handle(command, CancellationToken.None);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemBuilder.Posts, "new-post-in-posts.md"))
            .Should().BeTrue();
    }
}