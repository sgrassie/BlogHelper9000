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

        var result = sut.PublishPostDetailed("2024-01-01-a-post.md");

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

    [Fact]
    public void GetBlogInfo_Should_Include_MostRecentPost_In_LatestPosts()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024-01-01-post-1.md", new MockFileData("---\ntitle: Post 1\npublished: 01/01/2024\nispublished: true\n---"))
            .AddFile("/blog/_posts/2024-02-01-post-2.md", new MockFileData("---\ntitle: Post 2\npublished: 01/02/2024\nispublished: true\n---"))
            .AddFile("/blog/_posts/2024-03-01-post-3.md", new MockFileData("---\ntitle: Post 3\npublished: 01/03/2024\nispublished: true\n---"))
            .AddFile("/blog/_posts/2024-04-01-post-4.md", new MockFileData("---\ntitle: Post 4\npublished: 01/04/2024\nispublished: true\n---"))
            .AddFile("/blog/_posts/2024-05-01-post-5.md", new MockFileData("---\ntitle: Post 5\npublished: 01/05/2024\nispublished: true\n---"))
            .AddFile("/blog/_posts/2024-06-01-post-6.md", new MockFileData("---\ntitle: Post 6\npublished: 01/06/2024\nispublished: true\n---"))
            .AddFile("/blog/_posts/2024-07-01-post-7.md", new MockFileData("---\ntitle: Post 7\npublished: 01/07/2024\nispublished: true\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetBlogInfo();

        result.LatestPosts.Should().Contain(p => p.Title == "Post 7");
        result.LatestPosts!.First().Title.Should().Be("Post 7");
        result.LastPost!.Title.Should().Be("Post 7");
    }

    [Fact]
    public void GetBlogInfo_Should_Not_Count_Drafts_In_PostCount()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024-01-01-post-1.md", new MockFileData("---\ntitle: Post 1\npublished: 01/01/2024\nispublished: true\n---"))
            .AddFile("/blog/_drafts/a-draft.md", new MockFileData("---\ntitle: A draft\nispublished: false\n---"))
            .AddFile("/blog/_drafts/another-draft.md", new MockFileData("---\ntitle: Another draft\nispublished: false\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetBlogInfo();

        result.PostCount.Should().Be(1);
        result.UnPublishedCount.Should().Be(2);
    }

    [Fact]
    public void GetDraftDetails_Should_Extract_TitleAndWordCount()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-draft.md",
                new MockFileData("---\ntitle: A Draft\n---\n\nFive simple words in this body."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetDraftDetails();

        result.Should().ContainSingle();
        var detail = result[0];
        detail.FileName.Should().Be("a-draft.md");
        detail.FilePath.Should().Be("/blog/_drafts/a-draft.md");
        detail.Title.Should().Be("A Draft");
        detail.WordCount.Should().Be(6);
    }

    [Fact]
    public void GetDraftDetails_Should_ExtractReadinessFlags_FromBody()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-draft.md",
                new MockFileData("---\ntitle: A Draft\n---\n\nTODO: finish this. [placeholder image]"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetDraftDetails();

        result[0].ReadinessFlags.Should().BeEquivalentTo("TODO", "[placeholder");
    }

    [Fact]
    public void GetDraftDetails_Should_ReportHasFeaturedImage_WhenSet()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/with-image.md",
                new MockFileData("---\ntitle: Has Image\nfeatured_image: /assets/images/foo.webp\n---"))
            .AddFile("/blog/_drafts/without-image.md",
                new MockFileData("---\ntitle: No Image\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetDraftDetails();

        result.Single(d => d.FileName == "with-image.md").HasFeaturedImage.Should().BeTrue();
        result.Single(d => d.FileName == "without-image.md").HasFeaturedImage.Should().BeFalse();
    }

    [Fact]
    public void GetDraftDetails_Should_OrderByLastModifiedDescending()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/older.md", new MockFileData("---\ntitle: Older\n---"))
            .AddFile("/blog/_drafts/newer.md", new MockFileData("---\ntitle: Newer\n---"))
            .BuildFileSystem();
        fileSystem.File.SetLastWriteTime("/blog/_drafts/older.md", new DateTime(2024, 1, 1));
        fileSystem.File.SetLastWriteTime("/blog/_drafts/newer.md", new DateTime(2024, 6, 1));
        var sut = CreateSut(fileSystem);

        var result = sut.GetDraftDetails();

        result.Select(d => d.FileName).Should().Equal("newer.md", "older.md");
    }

    [Fact]
    public void GetDraftDetails_Should_ReturnEmpty_WhenNoDraftsDirectory()
    {
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetDraftDetails();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetDraftDetails_Should_FlagUnparseableFrontMatter_WithoutThrowing_AndStillReturnOtherDrafts()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/broken.md",
                new MockFileData("---\ntitle: Broken\nThis has no closing delimiter."))
            .AddFile("/blog/_drafts/fine.md",
                new MockFileData("---\ntitle: Fine\n---\n\nSome words here."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.GetDraftDetails();

        result.Should().HaveCount(2);

        var broken = result.Single(d => d.FileName == "broken.md");
        broken.Title.Should().BeNull();
        broken.WordCount.Should().Be(0);
        broken.HasFeaturedImage.Should().BeFalse();
        broken.ReadinessFlags.Should().Contain("unparseable front matter");

        var fine = result.Single(d => d.FileName == "fine.md");
        fine.Title.Should().Be("Fine");
        fine.ReadinessFlags.Should().NotContain("unparseable front matter");
    }

    [Fact]
    public void UnpublishPostDetailed_Should_RoundTrip_With_PublishPostDetailed()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(new DateTime(2024, 11, 1)));
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem, fakeTimeProvider);

        var addedPath = sut.AddPost("A Post", isDraft: true, content: "Body text.");
        addedPath.Should().NotBeNull();

        var publishResult = sut.PublishPostDetailed("a-post.md");
        publishResult.Outcome.Should().Be(Core.Models.PublishOutcome.Published);

        var unpublishResult = sut.UnpublishPostDetailed("2024-11-01-a-post.md");

        unpublishResult.Outcome.Should().Be(Core.Models.UnpublishOutcome.Unpublished);
        unpublishResult.DraftPath.Should().Be("/blog/_drafts/a-post.md");
        fileSystem.File.Exists("/blog/_drafts/a-post.md").Should().BeTrue();
        fileSystem.File.Exists(publishResult.PublishedPath!).Should().BeFalse();

        // The custom serialiser omits null values entirely, so the `published:` date key
        // disappears once PublishedOn is cleared; `ispublished:` remains as an explicit
        // `False` because IsPublished is a non-null bool the rest of the app relies on
        // (e.g. GetBlogInfo's `IsPublished == false` filter) to distinguish drafts from posts.
        var lines = fileSystem.File.ReadAllLines("/blog/_drafts/a-post.md");
        lines.Should().NotContain(l => l.Trim().StartsWith("published:"));
        lines.Should().Contain(l => l.Trim() == "ispublished: False");

        var handler = new MarkdownHandler(fileSystem);
        handler.GetBody("/blog/_drafts/a-post.md").Should().Be("Body text.");
    }

    [Fact]
    public void UnpublishPostDetailed_Should_Unpublish_NestedYearPost()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-05-01-old-post.md", new MockFileData("---\ntitle: Old post\npublished: 01/05/2024\nispublished: true\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.UnpublishPostDetailed("2024-05-01-old-post.md");

        result.Outcome.Should().Be(Core.Models.UnpublishOutcome.Unpublished);
        result.DraftPath.Should().Be("/blog/_drafts/old-post.md");
        fileSystem.File.Exists("/blog/_drafts/old-post.md").Should().BeTrue();
        fileSystem.File.Exists("/blog/_posts/2024/2024-05-01-old-post.md").Should().BeFalse();
    }

    [Fact]
    public void UnpublishPostDetailed_Should_Report_NotFound()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.UnpublishPostDetailed("nonexistent.md");

        result.Outcome.Should().Be(Core.Models.UnpublishOutcome.NotFound);
        result.DraftPath.Should().BeNull();
    }

    [Fact]
    public void UnpublishPostDetailed_Should_Report_NotPublished_ForDraft()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-draft.md", new MockFileData("---\ntitle: A draft\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.UnpublishPostDetailed("a-draft.md");

        result.Outcome.Should().Be(Core.Models.UnpublishOutcome.NotPublished);
        result.DraftPath.Should().BeNull();
    }

    [Fact]
    public void UnpublishPostDetailed_Should_Report_NotPublished_ForUndatedPostFile()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/undated.md", new MockFileData("---\ntitle: Undated\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.UnpublishPostDetailed("undated.md");

        result.Outcome.Should().Be(Core.Models.UnpublishOutcome.NotPublished);
    }

    [Fact]
    public void UnpublishPostDetailed_Should_Report_TargetExists()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-11-01-a-post.md", new MockFileData("---\ntitle: A post\npublished: 01/11/2024\nispublished: true\n---"))
            .AddFile("/blog/_drafts/a-post.md", new MockFileData("---\ntitle: Existing draft\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.UnpublishPostDetailed("2024-11-01-a-post.md");

        result.Outcome.Should().Be(Core.Models.UnpublishOutcome.TargetExists);
    }

    [Fact]
    public void UnpublishPostDetailed_DryRun_Should_ReportTarget_WithoutTouchingAnything()
    {
        const string original = "---\ntitle: A post\npublished: 01/11/2024\nispublished: true\n---";
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-11-01-a-post.md", new MockFileData(original))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.UnpublishPostDetailed("2024-11-01-a-post.md", dryRun: true);

        result.Outcome.Should().Be(Core.Models.UnpublishOutcome.Unpublished);
        result.DraftPath.Should().Be("/blog/_drafts/a-post.md");
        fileSystem.File.Exists("/blog/_posts/2024/2024-11-01-a-post.md").Should().BeTrue();
        fileSystem.File.Exists("/blog/_drafts/a-post.md").Should().BeFalse();
        fileSystem.File.ReadAllText("/blog/_posts/2024/2024-11-01-a-post.md").Should().Be(original);
    }

    [Fact]
    public void DeleteDraft_Should_Delete_DraftFile()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-draft.md", new MockFileData("---\ntitle: A draft\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.DeleteDraft("a-draft.md");

        result.Outcome.Should().Be(Core.Models.DeleteDraftOutcome.Deleted);
        result.FilePath.Should().Be("/blog/_drafts/a-draft.md");
        fileSystem.File.Exists("/blog/_drafts/a-draft.md").Should().BeFalse();
    }

    [Fact]
    public void DeleteDraft_Should_Report_NotFound()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.DeleteDraft("nonexistent.md");

        result.Outcome.Should().Be(Core.Models.DeleteDraftOutcome.NotFound);
        result.FilePath.Should().BeNull();
    }

    [Fact]
    public void DeleteDraft_Should_Report_NotADraft_ForPublishedPost_ByBareName()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.DeleteDraft("2024-01-01-a-post.md");

        result.Outcome.Should().Be(Core.Models.DeleteDraftOutcome.NotADraft);
        fileSystem.File.Exists("/blog/_posts/2024/2024-01-01-a-post.md").Should().BeTrue();
    }

    [Fact]
    public void DeleteDraft_Should_Report_NotADraft_ForPublishedPost_ByPath()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new MockFileData("---\ntitle: A post\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.DeleteDraft("/blog/_posts/2024/2024-01-01-a-post.md");

        result.Outcome.Should().Be(Core.Models.DeleteDraftOutcome.NotADraft);
        fileSystem.File.Exists("/blog/_posts/2024/2024-01-01-a-post.md").Should().BeTrue();
    }

    [Fact]
    public void DeleteDraft_DryRun_Should_LeaveFile_OnDisk()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/a-draft.md", new MockFileData("---\ntitle: A draft\n---"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.DeleteDraft("a-draft.md", dryRun: true);

        result.Outcome.Should().Be(Core.Models.DeleteDraftOutcome.Deleted);
        result.FilePath.Should().Be("/blog/_drafts/a-draft.md");
        fileSystem.File.Exists("/blog/_drafts/a-draft.md").Should().BeTrue();
    }

    [Fact]
    public void DeleteDraft_Should_Report_NotFound_ForPathOutsideBlogRoot()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.DeleteDraft("/etc/passwd");

        result.Outcome.Should().Be(Core.Models.DeleteDraftOutcome.NotFound);
    }
}
