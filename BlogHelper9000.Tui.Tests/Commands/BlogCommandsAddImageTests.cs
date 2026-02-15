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

public class BlogCommandsAddImageTests
{
    private readonly MockFileSystem _fs = new();
    private readonly IBlogService _blogService = Substitute.For<IBlogService>();
    private readonly ILogger<BlogCommands> _logger = Substitute.For<ILogger<BlogCommands>>();
    private readonly IUnsplashClient _unsplashClient = Substitute.For<IUnsplashClient>();
    private readonly IImageProcessor _imageProcessor = Substitute.For<IImageProcessor>();
    private readonly PostManager _postManager;
    private readonly BlogHelperOptions _options;

    public BlogCommandsAddImageTests()
    {
        _options = new BlogHelperOptions { BaseDirectory = "/blog" };
        _fs.AddDirectory("/blog/_posts");
        _fs.AddDirectory("/blog/_drafts");
        _fs.AddDirectory("/blog/assets/images");
        _postManager = new PostManager(
            _fs,
            new MarkdownHandler(_fs),
            Options.Create(_options));
    }

    private BlogCommands CreateSut() =>
        new(_blogService, _fs, _logger, _unsplashClient, _imageProcessor, _postManager, Options.Create(_options));

    [Fact]
    public void AddImage_AppearsInAllCommands()
    {
        BlogCommands.AllCommands
            .Should().Contain(c => c.Name == "Add Image");
    }

    [Fact]
    public void ExecuteAddImage_IsNoOp_WhenNoActiveFile()
    {
        var sut = CreateSut();
        sut.GetActiveFilePathCallback = () => null;

        var act = () => sut.ExecuteCommand("Add Image");

        act.Should().NotThrow();
    }

    [Fact]
    public void ExecuteAddImage_IsNoOp_WhenCallbackNotSet()
    {
        var sut = CreateSut();

        var act = () => sut.ExecuteCommand("Add Image");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AddImageAsync_CallsUnsplashAndImageProcessor()
    {
        var postContent = """
                          ---
                          title: Test Post
                          published: true
                          description: A test
                          tags: [test]
                          ---
                          Hello world
                          """;
        const string postPath = "/blog/_posts/2024-01-01-test-post.md";
        _fs.AddFile(postPath, new MockFileData(postContent));

        var imageStream = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);
        _unsplashClient.LoadImageAsync("coding").Returns(imageStream);

        var sut = CreateSut();
        var callbackInvoked = false;
        sut.FilesChangedCallback = () => callbackInvoked = true;

        _postManager.TryFindPost(postPath, out var markdownFile);
        markdownFile.Should().NotBeNull();

        sut.AddImageAsync(markdownFile!, "coding");

        // Allow the background task to complete
        await Task.Delay(500);

        await _unsplashClient.Received(1).LoadImageAsync("coding");
        await _imageProcessor.Received(1).Process(markdownFile!, imageStream, null);
    }

    [Fact]
    public async Task AddImageAsync_PassesBrandingPath_WhenConfigured()
    {
        var postContent = """
                          ---
                          title: Test Post
                          published: true
                          description: A test
                          tags: [test]
                          ---
                          Hello world
                          """;
        const string postPath = "/blog/_posts/2024-01-01-test-post.md";
        _fs.AddFile(postPath, new MockFileData(postContent));

        const string brandingFile = "/blog/assets/images/branding.png";
        _fs.AddFile(brandingFile, new MockFileData("logo"));

        var optionsWithBranding = new BlogHelperOptions
        {
            BaseDirectory = "/blog",
            AuthorBranding = "branding.png"
        };
        var postManagerWithBranding = new PostManager(
            _fs,
            new MarkdownHandler(_fs),
            Options.Create(optionsWithBranding));

        var imageStream = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);
        _unsplashClient.LoadImageAsync("coding").Returns(imageStream);

        var sut = new BlogCommands(
            _blogService, _fs, _logger,
            _unsplashClient, _imageProcessor,
            postManagerWithBranding, Options.Create(optionsWithBranding));

        postManagerWithBranding.TryFindPost(postPath, out var markdownFile);
        markdownFile.Should().NotBeNull();

        sut.AddImageAsync(markdownFile!, "coding");

        // Allow the background task to complete
        await Task.Delay(500);

        await _imageProcessor.Received(1).Process(markdownFile!, imageStream, brandingFile);
    }
}
