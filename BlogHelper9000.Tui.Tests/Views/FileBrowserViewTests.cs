using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tui.Tests.Views;

public class FileBrowserViewTests
{
    private static IOptions<BlogHelperOptions> CreateOptions(string baseDirectory = "/blog") =>
        Options.Create(new BlogHelperOptions { BaseDirectory = baseDirectory });

    [Fact]
    public void RefreshFiles_PopulatesFilePaths_ForDraftsAndPosts()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("draft content"))
            .AddFile("/blog/_posts/2024-01-15-hello.md", new MockFileData("post content"))
            .AddFile("/blog/_posts/2024-02-20-world.md", new MockFileData("post content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Expected layout:
        //   [_drafts]         -> null
        //   my-draft.md       -> /blog/_drafts/my-draft.md
        //   [_posts]          -> null
        //   2024-02-20-world  -> /blog/_posts/2024-02-20-world.md  (descending order)
        //   2024-01-15-hello  -> /blog/_posts/2024-01-15-hello.md
        view._filePaths.Should().HaveCount(5);
        view._filePaths[0].Should().BeNull("section header for _drafts");
        view._filePaths[1].Should().Be("/blog/_drafts/my-draft.md");
        view._filePaths[2].Should().BeNull("section header for _posts");
        view._filePaths[3].Should().Be("/blog/_posts/2024-02-20-world.md");
        view._filePaths[4].Should().Be("/blog/_posts/2024-01-15-hello.md");
    }

    [Fact]
    public void GetSelectedFilePath_ReturnsNull_WhenSelectionIsSectionHeader()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Index 0 is the [_drafts] header
        view._listView.SelectedItem = 0;

        view.GetSelectedFilePath().Should().BeNull();
    }

    [Fact]
    public void GetSelectedFilePath_ReturnsFullPath_ForDraftFile()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Index 1 is the first draft file
        view._listView.SelectedItem = 1;

        view.GetSelectedFilePath().Should().Be("/blog/_drafts/my-draft.md");
    }

    [Fact]
    public void GetSelectedFilePath_ReturnsFullPath_ForPostFile()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024-01-15-hello.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Index 0: [_drafts] header, Index 1: [_posts] header, Index 2: post file
        view._listView.SelectedItem = 2;

        view.GetSelectedFilePath().Should().Be("/blog/_posts/2024-01-15-hello.md");
    }

    [Fact]
    public void GetSelectedFilePath_ReturnsNull_WhenNothingSelected()
    {
        var fs = new JekyllBlogFilesystemBuilder().BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());

        // No RefreshFiles called, no items, SelectedItem should be null
        view.GetSelectedFilePath().Should().BeNull();
    }

    [Fact]
    public void GetSelectedFilePath_ReturnsFullPath_ForPostInSubdirectory()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_posts/2024/01/2024-01-15-nested.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Index 0: [_drafts], Index 1: [_posts], Index 2: nested post
        view._listView.SelectedItem = 2;

        view.GetSelectedFilePath().Should().Be("/blog/_posts/2024/01/2024-01-15-nested.md");
    }

    [Fact]
    public void EnterKey_FiresFileSelected_WithCorrectPath()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/test-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select the draft file (index 1)
        view._listView.SelectedItem = 1;

        string? selectedPath = null;
        view.FileSelected += path => selectedPath = path;

        view._listView.NewKeyDownEvent(new Terminal.Gui.Input.Key(Terminal.Gui.Drivers.KeyCode.Enter));

        selectedPath.Should().Be("/blog/_drafts/test-draft.md");
    }

    [Fact]
    public void EnterKey_DoesNotFireFileSelected_ForSectionHeader()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/test-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select section header (index 0)
        view._listView.SelectedItem = 0;

        bool eventFired = false;
        view.FileSelected += _ => eventFired = true;

        view._listView.NewKeyDownEvent(new Terminal.Gui.Input.Key(Terminal.Gui.Drivers.KeyCode.Enter));

        eventFired.Should().BeFalse();
    }

    [Fact]
    public void MarkFileModified_AddsAsteriskPrefix()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        view.MarkFileModified("/blog/_drafts/my-draft.md");

        // Index 1 is the draft file
        view._items[1].Should().Be("* my-draft.md");
    }

    [Fact]
    public void MarkFileSaved_RemovesAsteriskPrefix()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        view.MarkFileModified("/blog/_drafts/my-draft.md");
        view.MarkFileSaved("/blog/_drafts/my-draft.md");

        view._items[1].Should().Be("  my-draft.md");
    }

    [Fact]
    public void RefreshFiles_PreservesModifiedIndicator()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        view.MarkFileModified("/blog/_drafts/my-draft.md");
        view.RefreshFiles();

        view._items[1].Should().Be("* my-draft.md");
    }

    [Fact]
    public void MarkFileModified_IsNoOp_ForUnknownFile()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Should not throw
        view.MarkFileModified("/blog/_drafts/nonexistent.md");

        view._items[1].Should().Be("  my-draft.md");
    }
}
