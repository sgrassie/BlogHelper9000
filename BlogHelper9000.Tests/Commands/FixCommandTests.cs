using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Commands;

public class FixCommandTests
{
    private IOptions<BlogHelperOptions> _options;

    public FixCommandTests()
    {
        _options = Options.Create(new BlogHelperOptions
        {
            BaseDirectory = "./blog"
        });
    }

    [Fact]
    public void Should_AddPublishedOnFromDateInFilename_WhenPublishedOnIsMissing()
    {
        var header = """
                     ---
                     layout: post
                     description: test post
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);

        var command = new FixCommand
        {
            Status = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, blogService);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "published: 13/02/2024");
    }

    [Fact]
    public void Should_Update_PublishedOn_To_DateInFilename()
    {
        var header = """
                     ---
                     published: 01/01/2000
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);

        var command = new FixCommand
        {
            Status = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, blogService);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "published: 13/02/2024");
    }

    [Fact]
    public void Should_Update_Description_ToUseCorrectProperty()
    {
        var header = """
                     ---
                     layout: post
                     metadescription: writing-a-generic-plugin-manager-in-c
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();

        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);

        var command = new FixCommand
        {
            Description = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, blogService);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "description: writing-a-generic-plugin-manager-in-c");
    }

    [Fact]
    public void Should_Update_UpdateCategory_ToUseCorrectTagProperty()
    {
        var header = """
                     ---
                     layout: post
                     category: C#,C#,Coding,Plugin Manager
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);

        var command = new FixCommand
        {
            Tags = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, blogService);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "tags: ['C#','Coding','Plugin Manager']");
    }

    [Fact]
    public void Should_Update_UpdateCategories_ToUseCorrectTagProperty()
    {
        var header = """
                     ---
                     layout: post
                     categories: C#,C#,Coding,Plugin Manager
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();

        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var blogService = new BlogService(postManager, fileSystem, TimeProvider.System, NullLogger<BlogService>.Instance);

        var command = new FixCommand
        {
            Tags = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, blogService);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "tags: ['C#','Coding','Plugin Manager']");
    }
}
