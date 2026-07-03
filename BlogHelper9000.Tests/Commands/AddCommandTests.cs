using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Commands;

public class AddCommandTests
{
    private IOptions<BlogHelperOptions> _options;

    public AddCommandTests()
    {
        _options = Options.Create(new BlogHelperOptions
        {
            BaseDirectory = "./blog"
        });
    }

    [Fact]
    public async Task Should_Add_NewPost_AsDraft()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);
        var command = new AddCommand
        {
            Title = "New post in draft",
            IsDraft = true,
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, blogService);

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
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);
        var command = new AddCommand
        {
            Title = "New post in posts"
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, blogService);

        await sut.Handle(command, CancellationToken.None);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemBuilder.Posts, "new-post-in-posts.md"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_Add_NewPost_With_SuppliedTags()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);
        var command = new AddCommand
        {
            Title = "Tagged post",
            Tags = "csharp, dotnet"
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, blogService);

        await sut.Handle(command, CancellationToken.None);

        var path = fileSystem.Path.Combine(JekyllBlogFilesystemBuilder.Posts, "tagged-post.md");
        var header = new MarkdownHandler(fileSystem).LoadFile(path).Metadata;
        header.Tags.Should().Equal("csharp", "dotnet");
    }
}
