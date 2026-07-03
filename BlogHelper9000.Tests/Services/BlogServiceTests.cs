using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BlogHelper9000.Tests.Services;

public class BlogServiceTests
{
    private readonly IOptions<BlogHelperOptions> _options = Options.Create(new BlogHelperOptions
    {
        BaseDirectory = "/blog"
    });

    private BlogService CreateSut(System.IO.Abstractions.IFileSystem fileSystem, FakeTimeProvider? timeProvider = null)
    {
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        return new BlogService(postManager, fileSystem, timeProvider ?? new FakeTimeProvider(), NullLogger<BlogService>.Instance);
    }

    [Fact]
    public void AddPost_Should_Include_SuppliedTags()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var path = sut.AddPost("New Post", isDraft: true, tags: ["csharp", "dotnet"]);

        path.Should().NotBeNull();
        var header = new MarkdownHandler(fileSystem).LoadFile(path!).Metadata;
        header.Tags.Should().Equal("csharp", "dotnet");
    }

    [Fact]
    public void AddPost_Should_Return_Null_When_TargetFile_AlreadyExists()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder().BuildFileSystem();
        fileSystem.AddFile("/blog/_drafts/new-post.md", new MockFileData("---\ntitle: Existing\n---"));
        var sut = CreateSut(fileSystem);

        var path = sut.AddPost("New Post", isDraft: true);

        path.Should().BeNull();
        fileSystem.File.ReadAllText("/blog/_drafts/new-post.md").Should().Contain("Existing");
    }

    [Fact]
    public void PublishPost_Should_Return_Null_When_Post_Already_Published()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.PublishPost("2024-01-01-a-post.md");

        result.Should().BeNull();
    }

    [Fact]
    public void PublishPost_Should_Return_Null_When_TargetCollides()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 1)));
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .AddFile("/blog/_posts/2024/2024-11-01-a-post.md", new MockFileData("---\ntitle: Collision\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem, fakeTimeProvider);

        var result = sut.PublishPost("a-post.md");

        result.Should().BeNull();
        fileSystem.File.Exists("/blog/_drafts/a-post.md").Should().BeTrue();
    }

    [Fact]
    public void FixMetadata_Should_Skip_Malformed_File_And_Continue_With_Others()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/not-a-dated-filename.md", new MockFileData("---\ntitle: Bad\n---"))
            .AddFile("/blog/_posts/2024-01-01-good-post.md", new MockFileData("---\ntitle: Good\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var act = () => sut.FixMetadata(fixStatus: true, fixDescription: false, fixTags: false);

        act.Should().NotThrow();
        var goodHeader = new MarkdownHandler(fileSystem).LoadFile("/blog/_posts/2024-01-01-good-post.md").Metadata;
        goodHeader.PublishedOn.Should().Be(new DateTime(2024, 1, 1));
    }
}
