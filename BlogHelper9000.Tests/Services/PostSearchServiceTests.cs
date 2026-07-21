using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Services;

public class PostSearchServiceTests
{
    private readonly IOptions<BlogHelperOptions> _options = Options.Create(new BlogHelperOptions
    {
        BaseDirectory = "/blog"
    });

    private PostSearchService CreateSut(System.IO.Abstractions.IFileSystem fileSystem)
    {
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        return new PostSearchService(postManager);
    }

    [Fact]
    public void Search_Matches_Body_Text_In_A_Draft()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: A Draft\n---\n\nThis post talks about kubernetes networking."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        result.Matches.Should().ContainSingle(m => m.FileName == "my-draft.md");
        result.Matches.Single().IsDraft.Should().BeTrue();
    }

    [Fact]
    public void Search_Matches_Body_Text_In_A_Nested_Published_Post()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md",
                new MockFileData("---\ntitle: A Post\npublished: 01/01/2024\n---\n\nContent about kubernetes networking."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        result.Matches.Should().ContainSingle(m => m.FileName == "2024-01-01-a-post.md");
        result.Matches.Single().IsDraft.Should().BeFalse();
        result.Matches.Single().FilePath.Should().Be("/blog/_posts/2024/2024-01-01-a-post.md");
    }

    [Fact]
    public void Search_Matches_Query_In_Title()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: Kubernetes Basics\n---\n\nSome unrelated content."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        result.Matches.Should().ContainSingle(m => m.FileName == "my-draft.md");
    }

    [Fact]
    public void Search_Matches_Query_In_FrontMatter_Line()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md",
                new MockFileData("---\ntitle: A Draft\ndescription: All about kubernetes clusters\n---\n\nUnrelated body."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        result.Matches.Should().ContainSingle(m => m.FileName == "my-draft.md");
    }

    [Fact]
    public void Search_Is_CaseInsensitive()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: A Draft\n---\n\nAll about KUBERNETES clusters."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        result.Matches.Should().ContainSingle();
    }

    [Fact]
    public void Search_TagFilter_Alone_With_Empty_Query_Matches_Quoted_Stored_Tags()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/csharp-post.md", new MockFileData("---\ntitle: Csharp Post\ntags: ['Csharp']\n---\n\nBody."))
            .AddFile("/blog/_drafts/other-post.md", new MockFileData("---\ntitle: Other Post\ntags: [fsharp]\n---\n\nBody."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search(string.Empty, tag: "csharp");

        result.Matches.Should().ContainSingle(m => m.FileName == "csharp-post.md");
    }

    [Fact]
    public void Search_TagFilter_Combined_With_Query_Requires_Both_To_Match()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/csharp-post.md", new MockFileData("---\ntitle: Csharp Post\ntags: ['Csharp']\n---\n\nAbout kubernetes."))
            .AddFile("/blog/_drafts/other-tag-post.md", new MockFileData("---\ntitle: Other Post\ntags: [fsharp]\n---\n\nAbout kubernetes."))
            .AddFile("/blog/_drafts/no-match-post.md", new MockFileData("---\ntitle: No Match\ntags: ['Csharp']\n---\n\nUnrelated text."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes", tag: "csharp");

        result.Matches.Should().ContainSingle(m => m.FileName == "csharp-post.md");
    }

    [Fact]
    public void Search_IncludeDrafts_False_Excludes_Drafts()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: A Draft\n---\n\nAbout kubernetes."))
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md",
                new MockFileData("---\ntitle: A Post\npublished: 01/01/2024\n---\n\nAbout kubernetes."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes", includeDrafts: false);

        result.Matches.Should().ContainSingle(m => m.FileName == "2024-01-01-a-post.md");
    }

    [Fact]
    public void Search_Applies_Limit_But_Reports_TotalMatches_Before_Cap()
    {
        var builder = new JekyllBlogFilesystemBuilder();
        for (var i = 1; i <= 5; i++)
        {
            builder.AddFile($"/blog/_drafts/post-{i}.md", new MockFileData("---\ntitle: Post\n---\n\nAbout kubernetes."));
        }
        var fileSystem = builder.BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes", limit: 2);

        result.Matches.Should().HaveCount(2);
        result.TotalMatches.Should().Be(5);
    }

    [Fact]
    public void Search_Snippet_LineNumbers_Are_OneBased_And_Capped_At_Three_With_Trimmed_Text()
    {
        var longLine = new string('x', 200) + " kubernetes " + new string('y', 200);
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData(
                "---\n" +
                "title: A Draft\n" +
                "---\n" +
                "\n" +
                "kubernetes line one\n" +
                "kubernetes line two\n" +
                "kubernetes line three\n" +
                $"{longLine}\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        var match = result.Matches.Should().ContainSingle().Subject;
        match.Snippets.Should().HaveCount(3);
        match.Snippets[0].Line.Should().Be(5);
        match.Snippets[1].Line.Should().Be(6);
        match.Snippets[2].Line.Should().Be(7);
    }

    [Fact]
    public void Search_Snippet_Text_Is_Trimmed_To_Around_120_Chars_Centred_On_Hit()
    {
        var longLine = new string('x', 200) + "kubernetes" + new string('y', 200);
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData(
                $"---\ntitle: A Draft\n---\n\n{longLine}\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        var snippet = result.Matches.Single().Snippets.Single();
        snippet.Text.Length.Should().BeLessThanOrEqualTo(120);
        snippet.Text.Should().Contain("kubernetes");
    }

    [Fact]
    public void Search_With_No_Matches_Returns_Empty_Result()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: A Draft\n---\n\nUnrelated content."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search("kubernetes");

        result.Matches.Should().BeEmpty();
        result.TotalMatches.Should().Be(0);
    }

    [Fact]
    public void Search_With_Empty_Query_And_No_Tag_Returns_Empty_Result()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("---\ntitle: A Draft\n---\n\nSome content."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var result = sut.Search(string.Empty);

        result.Matches.Should().BeEmpty();
        result.TotalMatches.Should().Be(0);
    }
}
