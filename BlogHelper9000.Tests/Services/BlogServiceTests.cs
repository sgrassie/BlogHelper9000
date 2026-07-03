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

        // Full path required — see the note in PublishPostDetailed_Should_Report_AlreadyPublished.
        var result = sut.PublishPost("/blog/_posts/2024/2024-01-01-a-post.md");

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

        var result = sut.FixMetadata(fixStatus: true, fixDescription: false, fixTags: false);

        var goodHeader = new MarkdownHandler(fileSystem).LoadFile("/blog/_posts/2024-01-01-good-post.md").Metadata;
        goodHeader.PublishedOn.Should().Be(new DateTime(2024, 1, 1));
        result.Updated.Should().ContainSingle(f => f.EndsWith("2024-01-01-good-post.md"));
        result.Skipped.Should().ContainSingle(s => s.FilePath.EndsWith("not-a-dated-filename.md"));
    }

    [Fact]
    public void FixMetadata_Should_Not_Write_Files_When_DryRun()
    {
        const string original = "---\ntitle: Good\n---";
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024-01-01-good-post.md", new MockFileData(original))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.FixMetadata(fixStatus: true, fixDescription: false, fixTags: false, dryRun: true);

        fileSystem.File.ReadAllText("/blog/_posts/2024-01-01-good-post.md").Should().Be(original);
        result.Updated.Should().ContainSingle(f => f.EndsWith("2024-01-01-good-post.md"));
    }

    [Fact]
    public void AddPost_Should_Write_SuppliedContent_As_Body()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var path = sut.AddPost("New Post", isDraft: true, content: "Hello, world.");

        path.Should().NotBeNull();
        var handler = new MarkdownHandler(fileSystem);
        handler.GetBody(path!).Should().Be("Hello, world.");
    }

    [Fact]
    public void PublishPostDetailed_Should_Report_AlreadyPublished()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        // Note: TryFindPost only resolves bare filenames against _drafts/ and the immediate
        // _posts/ folder, not nested _posts/<year>/ subfolders — the full path is required here.
        var result = sut.PublishPostDetailed("/blog/_posts/2024/2024-01-01-a-post.md");

        result.Outcome.Should().Be(Core.Models.PublishOutcome.AlreadyPublished);
        result.PublishedPath.Should().BeNull();
    }

    [Fact]
    public void PublishPostDetailed_Should_Report_NotFound()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.PublishPostDetailed("nonexistent.md");

        result.Outcome.Should().Be(Core.Models.PublishOutcome.NotFound);
    }

    [Fact]
    public void PublishPostDetailed_Should_Report_TargetExists()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 1)));
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .AddFile("/blog/_posts/2024/2024-11-01-a-post.md", new MockFileData("---\ntitle: Collision\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem, fakeTimeProvider);

        var result = sut.PublishPostDetailed("a-post.md");

        result.Outcome.Should().Be(Core.Models.PublishOutcome.TargetExists);
    }

    [Fact]
    public void PublishPostDetailed_Should_Report_Published_OnSuccess()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 1)));
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem, fakeTimeProvider);

        var result = sut.PublishPostDetailed("a-post.md");

        result.Outcome.Should().Be(Core.Models.PublishOutcome.Published);
        result.PublishedPath.Should().EndWith("2024-11-01-a-post.md");
    }
}
