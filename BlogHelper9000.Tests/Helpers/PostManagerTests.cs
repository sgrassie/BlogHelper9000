using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Helpers;

public class PostManagerTests
{
    private readonly IOptions<BlogHelperOptions> _options = Options.Create(new BlogHelperOptions
    {
        BaseDirectory = "/blog"
    });

    [Fact]
    public void CreatePostPath_Should_Slugify_TraversalAttempt_Title()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var path = postManager.CreatePostPath("../../evil");

        path.Should().NotContain("..");
        path.Should().StartWith(JekyllBlogFilesystemBuilder.Posts);
    }

    [Fact]
    public void CreatePostPath_Should_Slugify_Title_With_Punctuation()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var path = postManager.CreatePostPath("My Post!");

        path.Should().Be(fileSystem.Path.Combine(JekyllBlogFilesystemBuilder.Posts, "my-post.md"));
    }

    [Fact]
    public void TryFindPost_Should_Reject_AbsolutePath_OutsideBlogRoot()
    {
        var fileSystem = (System.IO.Abstractions.TestingHelpers.MockFileSystem)new JekyllBlogFilesystemBuilder().BuildFileSystem();
        fileSystem.AddFile("/etc/passwd", new System.IO.Abstractions.TestingHelpers.MockFileData("root:x:0:0"));
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var found = postManager.TryFindPost("/etc/passwd", out var markdownFile);

        found.Should().BeFalse();
        markdownFile.Should().BeNull();
    }

    [Fact]
    public void TryFindAuthorBranding_Should_Reject_AbsolutePath_OutsideBlogRoot()
    {
        var fileSystem = (System.IO.Abstractions.TestingHelpers.MockFileSystem)new JekyllBlogFilesystemBuilder().BuildFileSystem();
        fileSystem.AddFile("/etc/passwd", new System.IO.Abstractions.TestingHelpers.MockFileData("root:x:0:0"));
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var found = postManager.TryFindAuthorBranding("/etc/passwd", out var brandingPath);

        found.Should().BeFalse();
        brandingPath.Should().BeEmpty();
    }

    [Fact]
    public void LoadYamlHeaderForAllPosts_Should_Not_Throw_When_PostAlreadyHas_OriginalFilenameKey()
    {
        const string header = """
                               ---
                               layout: post
                               originalFilename: some-other-name.md
                               lastUpdated: 01/01/2020 00:00:00
                               ---
                               """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/a-post.md", new System.IO.Abstractions.TestingHelpers.MockFileData(header))
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var act = () => postManager.LoadYamlHeaderForAllPosts();

        act.Should().NotThrow();
        var headers = postManager.LoadYamlHeaderForAllPosts();
        headers.Should().ContainSingle(h => h.Extras["originalFilename"] == "a-post.md");
    }

    [Fact]
    public void LoadYamlHeaderForAllPosts_Should_ReturnEmpty_When_DraftsAndPostsFolders_Missing()
    {
        var fileSystem = new System.IO.Abstractions.TestingHelpers.MockFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var headers = postManager.LoadYamlHeaderForAllPosts();

        headers.Should().BeEmpty();
    }

    [Fact]
    public void TryFindPost_Should_Resolve_BareFilename_Against_NestedYearFolder_Post()
    {
        const string header = "---\ntitle: A post\n---";
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new System.IO.Abstractions.TestingHelpers.MockFileData(header))
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var found = postManager.TryFindPost("2024-01-01-a-post.md", out var markdownFile);

        found.Should().BeTrue();
        markdownFile!.FilePath.Should().Be("/blog/_posts/2024/2024-01-01-a-post.md");
    }

    [Fact]
    public void TryFindPost_Should_Resolve_BareFilename_Against_NestedYearFolder_Draft()
    {
        const string header = "---\ntitle: A draft\n---";
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/2024/a-draft.md", new System.IO.Abstractions.TestingHelpers.MockFileData(header))
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var found = postManager.TryFindPost("a-draft.md", out var markdownFile);

        found.Should().BeTrue();
        markdownFile!.FilePath.Should().Be("/blog/_drafts/2024/a-draft.md");
    }

    [Fact]
    public void TryFindPost_Should_Still_Return_False_When_NoFileMatches_Anywhere()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new System.IO.Abstractions.TestingHelpers.MockFileData("---\ntitle: A post\n---"))
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var found = postManager.TryFindPost("does-not-exist.md", out var markdownFile);

        found.Should().BeFalse();
        markdownFile.Should().BeNull();
    }
}
