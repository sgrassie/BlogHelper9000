using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using BlogHelper9000.Tui.Commands;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BlogHelper9000.Tui.Tests.Commands;

public class BlogCommandsDeleteFileTests
{
    private readonly MockFileSystem _fs = new();
    private readonly IBlogService _blogService = Substitute.For<IBlogService>();
    private readonly ILogger<BlogCommands> _logger = Substitute.For<ILogger<BlogCommands>>();

    private BlogCommands CreateSut() => new(
        _blogService, _fs, _logger,
        Substitute.For<IUnsplashClient>(),
        Substitute.For<IImageProcessor>(),
        new PostManager(_fs, new MarkdownHandler(_fs), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" })),
        Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));

    [Fact]
    public void DeleteFile_AppearsInAllCommands()
    {
        BlogCommands.AllCommands
            .Should().Contain(c => c.Name == "Delete File");
    }

    [Fact]
    public void DeleteFileAt_DeletesFileAndInvokesCallback()
    {
        const string path = "/blog/_posts/2024-01-01-test.md";
        _fs.AddFile(path, new MockFileData("content"));

        var sut = CreateSut();
        var callbackInvoked = false;
        sut.FilesChangedCallback = () => callbackInvoked = true;

        sut.DeleteFileAt(path);

        _fs.File.Exists(path).Should().BeFalse();
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void DeleteFileAt_IsNoOp_WhenFileDoesNotExist()
    {
        var sut = CreateSut();
        var callbackInvoked = false;
        sut.FilesChangedCallback = () => callbackInvoked = true;

        var act = () => sut.DeleteFileAt("/nonexistent/file.md");

        act.Should().NotThrow();
        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    public void ExecuteDeleteFile_IsNoOp_WhenNoFileSelected()
    {
        var sut = CreateSut();
        sut.GetSelectedFilePathCallback = () => null;

        var act = () => sut.ExecuteCommand("Delete File");

        act.Should().NotThrow();
    }

    [Fact]
    public void ExecuteDeleteFile_IsNoOp_WhenCallbackNotSet()
    {
        var sut = CreateSut();

        var act = () => sut.ExecuteCommand("Delete File");

        act.Should().NotThrow();
    }
}
