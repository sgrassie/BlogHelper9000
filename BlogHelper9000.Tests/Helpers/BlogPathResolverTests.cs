using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core.Helpers;

namespace BlogHelper9000.Tests.Helpers;

public class BlogPathResolverTests
{
    [Fact]
    public void Should_Reject_AbsolutePath_OutsideBase()
    {
        var fileSystem = new MockFileSystem();
        var sut = new BlogPathResolver(fileSystem, "/blog");

        var result = sut.TryResolveWithinBase("/etc/passwd", out var resolved);

        result.Should().BeFalse();
        resolved.Should().BeEmpty();
    }

    [Fact]
    public void Should_Reject_RelativeTraversal_OutsideBase()
    {
        var fileSystem = new MockFileSystem();
        var sut = new BlogPathResolver(fileSystem, "/blog");

        var result = sut.TryResolveWithinBase("../../etc/passwd", out var resolved);

        result.Should().BeFalse();
    }

    [Fact]
    public void Should_Accept_PlainFilename_ResolvedAgainstBase()
    {
        var fileSystem = new MockFileSystem();
        var sut = new BlogPathResolver(fileSystem, "/blog/_posts");

        var result = sut.TryResolveWithinBase("post.md", out var resolved);

        result.Should().BeTrue();
        resolved.Should().Be(fileSystem.Path.GetFullPath("/blog/_posts/post.md"));
    }

    [Fact]
    public void Should_Accept_PathAlreadyInsideBase()
    {
        var fileSystem = new MockFileSystem();
        var sut = new BlogPathResolver(fileSystem, "/blog");

        var result = sut.TryResolveWithinBase("/blog/_posts/post.md", out var resolved);

        result.Should().BeTrue();
        resolved.Should().Be(fileSystem.Path.GetFullPath("/blog/_posts/post.md"));
    }

    [Fact]
    public void Should_Reject_SiblingDirectory_ThatSharesBaseAsPrefix()
    {
        var fileSystem = new MockFileSystem();
        var sut = new BlogPathResolver(fileSystem, "/blog");

        var result = sut.TryResolveWithinBase("/blog-secrets/passwords.md", out var resolved);

        result.Should().BeFalse();
    }
}
