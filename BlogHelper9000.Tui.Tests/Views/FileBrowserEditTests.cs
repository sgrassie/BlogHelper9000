using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tui.Tests.Views;

public class FileBrowserEditTests
{
    private static IOptions<BlogHelperOptions> CreateOptions(string baseDirectory = "/blog") =>
        Options.Create(new BlogHelperOptions { BaseDirectory = baseDirectory });

    [Fact]
    public void CopySelectedFile_ThenPaste_CopiesFileToSameDirectory()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("draft content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select the draft file (index 1)
        view._listView.SelectedItem = 1;

        view.CopySelectedFile();
        view.PasteFile();

        fs.File.Exists("/blog/_drafts/my-draft.md").Should().BeTrue("original file should still exist");
        fs.File.Exists("/blog/_drafts/my-draft-1.md").Should().BeTrue("copy should be created with disambiguated name");
        view._filePaths.Should().Contain("/blog/_drafts/my-draft-1.md");
    }

    [Fact]
    public void CutSelectedFile_ThenPaste_MovesFile()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("draft content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select the draft file (index 1)
        view._listView.SelectedItem = 1;

        view.CutSelectedFile();

        // Select [_posts] header (index 2) to paste there
        view._listView.SelectedItem = 2;

        view.PasteFile();

        fs.File.Exists("/blog/_drafts/my-draft.md").Should().BeFalse("file should be moved from original location");
        fs.File.Exists("/blog/_posts/my-draft.md").Should().BeTrue("file should exist in new location");
    }

    [Fact]
    public void PasteFile_IsNoOp_WhenClipboardEmpty()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        var fileCountBefore = view._filePaths.Count;

        // Should not throw
        view.PasteFile();

        view._filePaths.Should().HaveCount(fileCountBefore);
    }

    [Fact]
    public void DeleteFile_RemovesFile()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        view.DeleteFile("/blog/_drafts/my-draft.md");

        fs.File.Exists("/blog/_drafts/my-draft.md").Should().BeFalse();
        view._filePaths.Should().NotContain("/blog/_drafts/my-draft.md");
    }

    [Fact]
    public void CopySelectedFile_IsNoOp_WhenSectionHeaderSelected()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select section header (index 0)
        view._listView.SelectedItem = 0;

        view.CopySelectedFile();

        view._clipboard.Should().BeNull("copying a section header should be a no-op");
    }

    [Fact]
    public void CutSelectedFile_ClearsClipboard_AfterPaste()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select the draft file and cut
        view._listView.SelectedItem = 1;
        view.CutSelectedFile();

        // Paste into same directory
        view.PasteFile();
        view._clipboard.Should().BeNull("clipboard should be cleared after cut-paste");

        // Count files before second paste
        var fileCountBefore = view._filePaths.Count;

        // Second paste should be a no-op
        view.PasteFile();
        view._filePaths.Should().HaveCount(fileCountBefore);
    }

    [Fact]
    public void PasteFile_DisambiguatesName_WhenTargetExists()
    {
        var fs = new JekyllBlogFilesystemBuilder()
            .AddFile("/blog/_drafts/my-draft.md", new MockFileData("content"))
            .BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Select and copy
        view._listView.SelectedItem = 1;
        view.CopySelectedFile();

        // First paste → my-draft-1.md
        view._listView.SelectedItem = 1;
        view.PasteFile();
        fs.File.Exists("/blog/_drafts/my-draft-1.md").Should().BeTrue();

        // Re-select a file in drafts after refresh, then paste again → my-draft-2.md
        view._listView.SelectedItem = 1;
        view.PasteFile();
        fs.File.Exists("/blog/_drafts/my-draft-2.md").Should().BeTrue();
    }

    [Fact]
    public void DeleteFile_IsNoOp_WhenFileDoesNotExist()
    {
        var fs = new JekyllBlogFilesystemBuilder().BuildFileSystem();

        var view = new Tui.Views.FileBrowserView(fs, CreateOptions());
        view.RefreshFiles();

        // Should not throw
        view.DeleteFile("/blog/_drafts/nonexistent.md");
    }
}
