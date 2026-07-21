using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class ValidatePostToolTests
{
    private static PostValidator CreateValidator(MockFileSystem fileSystem, string baseDirectory = "/blog")
    {
        var options = Options.Create(new BlogHelperOptions { BaseDirectory = baseDirectory });
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), options);
        return new PostValidator(postManager);
    }

    [Fact]
    public void ValidatePost_CleanPost_ReturnsOk_ValidTrue_NoFindings()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/clean.md", new MockFileData(
            "---\n" +
            "title: A Clean Draft\n" +
            "description: All about it\n" +
            "tags: [csharp, dotnet]\n" +
            "---\n\n" +
            "Some plain body text with no links or images."));
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidatePost(validator, "clean.md");

        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeTrue();
        result.Data.Findings.Should().BeEmpty();
        result.Data.FilePath.Should().Be("/blog/_drafts/clean.md");
    }

    [Fact]
    public void ValidatePost_MissingImageAndTodo_ReturnsOk_ValidFalse_WithFindings()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_posts");
        fileSystem.AddFile("/blog/_drafts/messy.md", new MockFileData(
            "---\n" +
            "title: A Messy Draft\n" +
            "description: All about it\n" +
            "tags: [csharp]\n" +
            "---\n\n" +
            "![missing image](/img/missing.png)\n\n" +
            "TODO: finish this section\n"));
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidatePost(validator, "messy.md");

        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();

        var imageFinding = result.Data.Findings.Should().ContainSingle(f => f.Check == "image-missing").Subject;
        imageFinding.Severity.Should().Be("Error");
        imageFinding.Line.Should().NotBeNull();

        var placeholderFinding = result.Data.Findings.Should().ContainSingle(f => f.Check == "placeholder").Subject;
        placeholderFinding.Severity.Should().Be("Warning");
        placeholderFinding.Message.Should().Contain("TODO");
    }

    [Fact]
    public void ValidatePost_UnknownPost_ReturnsFailureEnvelope_NamingThePost()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidatePost(validator, "nonexistent.md");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("nonexistent.md");
    }

    [Fact]
    public void ValidateBlog_Default_ReturnsAllFiles_WithCorpusCounts()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/clean.md", new MockFileData(
            "---\ntitle: Clean\ndescription: D\ntags: [csharp]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_drafts/warn-only.md", new MockFileData(
            "---\ntitle: Warn Only\ntags: [csharp]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_posts/2024-01-01-error-post.md", new MockFileData(
            "---\ndescription: D\ntags: [csharp]\n---\n\nBody."));
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidateBlog(validator, null);

        result.Success.Should().BeTrue();
        result.Data!.FilesChecked.Should().Be(3);
        result.Data.Files.Should().HaveCount(2); // clean.md has no findings, omitted
        result.Data.Errors.Should().Be(1); // missing title on error-post
        result.Data.Warnings.Should().Be(1); // missing description on warn-only
    }

    [Fact]
    public void ValidateBlog_MinimumSeverityError_ExcludesWarningOnlyFiles_ButCountsThemInTotals()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/warn-only.md", new MockFileData(
            "---\ntitle: Warn Only\ntags: [csharp]\n---\n\nBody."));
        fileSystem.AddFile("/blog/_posts/2024-01-01-error-post.md", new MockFileData(
            "---\ndescription: D\ntags: [csharp]\n---\n\nBody."));
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidateBlog(validator, "Error");

        result.Success.Should().BeTrue();
        result.Data!.FilesChecked.Should().Be(2);
        result.Data.Warnings.Should().Be(1);
        result.Data.Errors.Should().Be(1);
        result.Data.Files.Should().ContainSingle();
        result.Data.Files[0].FilePath.Should().Be("/blog/_posts/2024-01-01-error-post.md");
        result.Data.Files[0].Findings.Should().OnlyContain(f => f.Severity == "Error");
    }

    [Fact]
    public void ValidateBlog_InvalidMinimumSeverity_ReturnsFailureListingValidValues()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidateBlog(validator, "Critical");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Info");
        result.Error.Should().Contain("Warning");
        result.Error.Should().Contain("Error");
    }

    [Fact]
    public void ValidateBlog_NumericMinimumSeverity_ReturnsFailureListingValidValues()
    {
        // Enum.TryParse accepts the underlying numeric value (e.g. "5") and silently produces
        // an undefined enum member — this must be rejected the same as any other bad input.
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/blog/_drafts");
        fileSystem.AddDirectory("/blog/_posts");
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidateBlog(validator, "5");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("5");
    }

    [Fact]
    public void ValidateBlog_BrokenFrontMatterFile_DoesNotFailTheSweep()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/blog/_drafts/broken.md",
            new MockFileData("---\ntitle: Broken\nThis has no closing delimiter."));
        fileSystem.AddFile("/blog/_drafts/clean.md", new MockFileData(
            "---\ntitle: Clean\ndescription: D\ntags: [csharp]\n---\n\nBody."));
        var validator = CreateValidator(fileSystem);

        var result = ValidatePostTool.ValidateBlog(validator, null);

        result.Success.Should().BeTrue();
        var brokenFile = result.Data!.Files.Should().ContainSingle(f => f.FilePath == "/blog/_drafts/broken.md").Subject;
        brokenFile.Findings.Should().ContainSingle(f => f.Check == "front-matter" && f.Severity == "Error");
        brokenFile.Valid.Should().BeFalse();
    }
}
