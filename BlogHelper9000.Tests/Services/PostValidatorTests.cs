using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BlogHelper9000.Tests.Services;

public class PostValidatorTests
{
    private readonly IOptions<BlogHelperOptions> _options = Options.Create(new BlogHelperOptions
    {
        BaseDirectory = "/blog"
    });

    private PostValidator CreateSut(IFileSystem fileSystem)
    {
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        return new PostValidator(postManager);
    }

    [Fact]
    public void ValidatePost_Clean_Draft_Has_No_Findings()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/clean-draft.md", new MockFileData(
                "---\n" +
                "title: A Clean Draft\n" +
                "description: All about kubernetes clusters\n" +
                "tags: [csharp, dotnet]\n" +
                "---\n\n" +
                "Some plain body text with no links or images."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("clean-draft.md");

        report.Findings.Should().BeEmpty();
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePost_Broken_FrontMatter_Yields_Single_Error_Without_Crashing()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/broken.md",
                new MockFileData("---\ntitle: Broken\nThis has no closing delimiter."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("broken.md");

        report.Findings.Should().ContainSingle();
        var finding = report.Findings.Single();
        finding.Severity.Should().Be(ValidationSeverity.Error);
        finding.Check.Should().Be("front-matter");
        finding.Message.Should().Contain("front matter does not parse");
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidatePost_Missing_Title_Is_Error()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/no-title.md", new MockFileData(
                "---\n" +
                "description: A description\n" +
                "tags: [csharp]\n" +
                "---\n\n" +
                "Body text."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("no-title.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "front-matter" && f.Message.Contains("title"));
    }

    [Fact]
    public void ValidatePost_Missing_Description_Is_Warning()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/no-description.md", new MockFileData(
                "---\n" +
                "title: A Title\n" +
                "tags: [csharp]\n" +
                "---\n\n" +
                "Body text."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("no-description.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning && f.Check == "front-matter" && f.Message.Contains("description"));
    }

    [Fact]
    public void ValidatePost_Empty_Tags_Is_Warning()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/no-tags.md", new MockFileData(
                "---\n" +
                "title: A Title\n" +
                "description: A description\n" +
                "tags: []\n" +
                "---\n\n" +
                "Body text."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("no-tags.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning && f.Check == "front-matter" && f.Message.Contains("tags"));
    }

    [Fact]
    public void ValidatePost_Missing_FeaturedImage_Is_Error()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/bad-image.md", new MockFileData(
                "---\n" +
                "title: T\n" +
                "description: D\n" +
                "tags: [a]\n" +
                "featured_image: /assets/images/x.webp\n" +
                "---\n\n" +
                "Body text."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("bad-image.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "image-missing" && f.Message.Contains("/assets/images/x.webp"));
    }

    [Fact]
    public void ValidatePost_Existing_FeaturedImage_Is_Clean()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/good-image.md", new MockFileData(
                "---\n" +
                "title: T\n" +
                "description: D\n" +
                "tags: [a]\n" +
                "featured_image: /assets/images/x.webp\n" +
                "---\n\n" +
                "Body text."))
            .AddFile("/blog/assets/images/x.webp", new MockFileData("fake-image-bytes"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("good-image.md");

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePost_Https_FeaturedImage_Is_Skipped()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/remote-image.md", new MockFileData(
                "---\n" +
                "title: T\n" +
                "description: D\n" +
                "tags: [a]\n" +
                "featured_image: https://example.com/x.webp\n" +
                "---\n\n" +
                "Body text."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("remote-image.md");

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePost_Inline_Missing_Image_Is_Error_With_Line_Number()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/inline-image.md", new MockFileData(
                "---\n" +          // line 1
                "title: T\n" +     // line 2
                "description: D\n" + // line 3
                "tags: [a]\n" +    // line 4
                "---\n\n" +        // line 5, 6
                "Some intro text.\n\n" + // line 7, 8
                "![missing](/assets/images/missing.png)\n")) // line 9
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("inline-image.md");

        var finding = report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "image-missing").Subject;
        finding.Line.Should().Be(9);
    }

    [Fact]
    public void ValidatePost_Relative_Inline_Image_Is_Info()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/relative-image.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "![rel](images/relative.png)\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("relative-image.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Info && f.Check == "image-missing");
    }

    [Fact]
    public void ValidatePost_Internal_Link_To_Existing_Nested_Post_Slug_Is_Clean()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/2024-05-01-real-post.md", new MockFileData(
                "---\ntitle: Real Post\ndescription: D\ntags: [a]\npublished: 01/05/2024\n---\n\nContent."))
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "Check out [this post](/2024/05/01/real-post/) for details.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("linker.md");

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePost_Unresolved_Internal_Link_Is_Warning()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "Check out [this post](/does-not-exist/) for details.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("linker.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning && f.Check == "internal-link");
    }

    [Fact]
    public void ValidatePost_PostUrl_Liquid_Tag_Valid_Name_Is_Clean()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024-05-01-real-post.md", new MockFileData(
                "---\ntitle: Real Post\ndescription: D\ntags: [a]\npublished: 01/05/2024\n---\n\nContent."))
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "See {% post_url 2024-05-01-real-post %} for details.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("linker.md");

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePost_PostUrl_Liquid_Tag_Invalid_Name_Is_Error()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "See {% post_url 2024-05-01-nonexistent-post %} for details.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("linker.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "liquid-link");
    }

    [Fact]
    public void ValidatePost_Link_Liquid_Tag_Existing_Path_Is_Clean()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/about.md", new MockFileData("---\ntitle: About\n---\n\nAbout page."))
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "See {% link about.md %} for details.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("linker.md");

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePost_Link_Liquid_Tag_Missing_Path_Is_Error()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "See {% link missing.md %} for details.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("linker.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "liquid-link");
    }

    [Fact]
    public void ValidatePost_Empty_Link_Target_Is_Error()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/placeholder.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "This is a [placeholder link]() to fix later.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("placeholder.md");

        report.Findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "placeholder");
    }

    [Fact]
    public void ValidatePost_Bare_Github_Profile_Link_Is_Warning()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/github-link.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "Introduction to Oakton [here](https://github.com/someuser).\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("github-link.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning &&
            f.Check == "placeholder" &&
            f.Message.Contains("GitHub profile"));
    }

    [Fact]
    public void ValidatePost_Full_Github_Repo_Link_Is_Clean()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/github-link.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "See the [repo](https://github.com/someuser/somerepo) for source.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("github-link.md");

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePost_Todo_Marker_In_Body_Is_Warning()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/todo.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "TODO: finish this section properly.\n"))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("todo.md");

        report.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning && f.Check == "placeholder" && f.Message.Contains("TODO"));
    }

    [Fact]
    public void ValidatePost_Unknown_Post_Returns_Single_Error_Report()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var report = sut.ValidatePost("does-not-exist.md");

        report.FilePath.Should().Be("does-not-exist.md");
        report.Findings.Should().ContainSingle();
        report.Findings.Single().Severity.Should().Be(ValidationSeverity.Error);
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateBlog_Returns_One_Report_Per_File_And_Survives_A_Broken_File()
    {
        var fileSystem = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/clean.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\nClean body."))
            .AddFile("/blog/_drafts/broken.md",
                new MockFileData("---\ntitle: Broken\nNo closing delimiter here."))
            .AddFile("/blog/_posts/2024/2024-01-01-a-post.md", new MockFileData(
                "---\ntitle: A Post\ndescription: D\ntags: [a]\npublished: 01/01/2024\n---\n\nContent."))
            .BuildFileSystem();
        var sut = CreateSut(fileSystem);

        var reports = sut.ValidateBlog();

        reports.Should().HaveCount(3);
        reports.Should().ContainSingle(r => r.FilePath.Contains("broken.md") && !r.IsValid &&
            r.Findings.Single().Check == "front-matter");
        reports.Where(r => !r.FilePath.Contains("broken.md")).Should().OnlyContain(r => r.IsValid);
    }

    [Fact]
    public void ValidateBlog_Survives_Directory_Enumeration_Failure_And_Degrades_SlugIndex()
    {
        // Simulates a directory-level IO failure (permissions, concurrent delete) while
        // enumerating _posts. Both guards under test share this one fault: ValidateBlog's
        // file-collection guard (which must turn the failure into a finding instead of an
        // escaping exception) and BuildSlugIndex's guard (which also enumerates _posts and
        // must degrade to an empty index rather than throw, since it runs on the same call).
        var inner = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/linker.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\n---\n\n" +
                "See {% post_url 2024-05-01-real-post %} for details.\n"))
            .BuildFileSystem();
        var fileSystem = CreateFileSystemThrowingOnDirectoryEnumeration(inner, JekyllBlogFilesystemBuilder.Posts);
        var sut = CreateSut(fileSystem);

        var reports = sut.ValidateBlog();

        reports.Should().HaveCount(2);

        var directoryFailureReport = reports.Should().ContainSingle(r => r.FilePath == JekyllBlogFilesystemBuilder.Posts).Subject;
        directoryFailureReport.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "validator-error");

        // BuildSlugIndex degraded to an empty index (rather than throwing), so the
        // post_url check — which would otherwise resolve against real _posts filenames —
        // now conservatively reports every reference as unresolved.
        var draftReport = reports.Single(r => r.FilePath.Contains("linker.md"));
        draftReport.Findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Error && f.Check == "liquid-link");
    }

    [Fact]
    public void ValidateFile_Unexpected_Exception_In_Later_Checks_Gets_ValidatorError_Not_FrontMatter()
    {
        // The featured_image existence check (run well after front matter has already
        // parsed successfully) is where we inject the fault, so the outer catch — not the
        // LoadFile-specific one — must be the one that handles it, and it must be labelled
        // distinctly from a front-matter parse failure.
        var inner = (MockFileSystem)new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/bad-image.md", new MockFileData(
                "---\ntitle: T\ndescription: D\ntags: [a]\nfeatured_image: /assets/images/x.webp\n---\n\nBody."))
            .BuildFileSystem();
        var failingPath = inner.Path.Combine("/blog", "assets/images/x.webp");
        var fileSystem = CreateFileSystemThrowingOnFileExists(inner, failingPath);
        var sut = CreateSut(fileSystem);

        var reports = sut.ValidateBlog();

        var report = reports.Should().ContainSingle().Subject;
        var finding = report.Findings.Should().ContainSingle().Subject;
        finding.Severity.Should().Be(ValidationSeverity.Error);
        finding.Check.Should().Be("validator-error");
        finding.Message.Should().NotContain("front matter");
    }

    // --- Fault-injection helpers -------------------------------------------------------
    // NSubstitute wrappers around a real MockFileSystem: delegate everything to the inner
    // filesystem except the one call site under test, which throws. Only the members
    // PostManager/PostValidator actually touch need configuring.

    private static IFileSystem CreateFileSystemThrowingOnDirectoryEnumeration(MockFileSystem inner, string failingDirectory)
    {
        var fakeFs = Substitute.For<IFileSystem>();
        fakeFs.Path.Returns(inner.Path);
        fakeFs.File.Returns(inner.File);
        fakeFs.FileInfo.Returns(inner.FileInfo);
        fakeFs.DirectoryInfo.Returns(inner.DirectoryInfo);
        fakeFs.FileStream.Returns(inner.FileStream);
        fakeFs.FileSystemWatcher.Returns(inner.FileSystemWatcher);
        fakeFs.DriveInfo.Returns(inner.DriveInfo);

        var throwingDirectory = Substitute.For<IDirectory>();
        throwingDirectory.Exists(Arg.Any<string>()).Returns(ci => inner.Directory.Exists(ci.Arg<string>()));
        throwingDirectory.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(ci =>
            {
                var path = ci.ArgAt<string>(0);
                if (path == failingDirectory)
                    throw new IOException($"simulated failure enumerating '{path}'");
                return inner.Directory.EnumerateFiles(path, ci.ArgAt<string>(1), ci.ArgAt<SearchOption>(2));
            });

        fakeFs.Directory.Returns(throwingDirectory);
        return fakeFs;
    }

    private static IFileSystem CreateFileSystemThrowingOnFileExists(MockFileSystem inner, string failingPath)
    {
        var fakeFs = Substitute.For<IFileSystem>();
        fakeFs.Path.Returns(inner.Path);
        fakeFs.Directory.Returns(inner.Directory);
        fakeFs.FileInfo.Returns(inner.FileInfo);
        fakeFs.DirectoryInfo.Returns(inner.DirectoryInfo);
        fakeFs.FileStream.Returns(inner.FileStream);
        fakeFs.FileSystemWatcher.Returns(inner.FileSystemWatcher);
        fakeFs.DriveInfo.Returns(inner.DriveInfo);

        var throwingFile = Substitute.For<IFile>();
        throwingFile.Exists(Arg.Any<string>()).Returns(ci =>
        {
            var path = ci.Arg<string>();
            if (path == failingPath)
                throw new IOException($"simulated failure checking existence of '{path}'");
            return inner.File.Exists(path);
        });
        throwingFile.ReadAllLines(Arg.Any<string>()).Returns(ci => inner.File.ReadAllLines(ci.Arg<string>()));

        fakeFs.File.Returns(throwingFile);
        return fakeFs;
    }
}
