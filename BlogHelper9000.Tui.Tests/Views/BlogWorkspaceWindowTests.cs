using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using BlogHelper9000.TestHelpers;
using BlogHelper9000.Tui.Commands;
using BlogHelper9000.Tui.Views;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BlogHelper9000.Tui.Tests.Views;

public class BlogWorkspaceWindowTests
{
    private static IOptions<BlogHelperOptions> CreateOptions(string baseDirectory = "/blog") =>
        Options.Create(new BlogHelperOptions { BaseDirectory = baseDirectory });

    private static BlogWorkspaceWindow CreateFallbackWorkspace(IFileSystem? fileSystem = null)
    {
        fileSystem ??= new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("draft"))
            .BuildFileSystem();

        var fileBrowser = new FileBrowserView(fileSystem, CreateOptions());
        var editor = new EditorSurface(fileSystem);
        var options = CreateOptions();
        var blogCommands = new BlogCommands(
            Substitute.For<IBlogService>(),
            fileSystem,
            NullLogger<BlogCommands>.Instance,
            Substitute.For<IUnsplashClient>(),
            Substitute.For<IImageProcessor>(),
            new PostManager(fileSystem, new MarkdownHandler(fileSystem), options),
            options);
        var commandPalette = new CommandPalette(blogCommands);
        var logger = NullLogger<BlogWorkspaceWindow>.Instance;

        return new BlogWorkspaceWindow(fileBrowser, editor, blogCommands, commandPalette, logger);
    }

    [Fact]
    public void HandleTabFocusToggle_ReturnsFalse_WhenBrowserHidden()
    {
        var workspace = CreateFallbackWorkspace();

        // Hide the browser
        workspace.ToggleFileBrowser();

        workspace.HandleTabFocusToggle().Should().BeFalse();
    }

    [Fact]
    public void HandleTabFocusToggle_ReturnsTrue_WhenBrowserVisible()
    {
        var workspace = CreateFallbackWorkspace();

        // Browser is visible by default
        workspace.HandleTabFocusToggle().Should().BeTrue();
    }

    [Fact]
    public void HandleTabFocusToggle_ReturnsFalse_AfterToggleShowThenHide()
    {
        var workspace = CreateFallbackWorkspace();

        // Hide, then show, then hide again
        workspace.ToggleFileBrowser(); // hide
        workspace.ToggleFileBrowser(); // show
        workspace.ToggleFileBrowser(); // hide

        workspace.HandleTabFocusToggle().Should().BeFalse();
    }

    [Fact]
    public void HandleTabFocusToggle_ReturnsTrue_AfterReShowingBrowser()
    {
        var workspace = CreateFallbackWorkspace();

        workspace.ToggleFileBrowser(); // hide
        workspace.ToggleFileBrowser(); // show

        workspace.HandleTabFocusToggle().Should().BeTrue();
    }
}
