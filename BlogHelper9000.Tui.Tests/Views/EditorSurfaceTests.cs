using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Tui.Views;
using FluentAssertions;

namespace BlogHelper9000.Tui.Tests.Views;

public class EditorSurfaceTests
{
    [Fact]
    public void IsModified_Is_False_Immediately_After_LoadFile()
    {
        var fs = new MockFileSystem();
        fs.AddFile("/blog/post.md", new MockFileData("original content"));
        var sut = new EditorSurface(fs);

        sut.LoadFile("/blog/post.md");

        sut.IsModified.Should().BeFalse();
    }

    [Fact]
    public void Save_Writes_Buffer_Contents_Back_To_The_Loaded_File()
    {
        var fs = new MockFileSystem();
        fs.AddFile("/blog/post.md", new MockFileData("original content"));
        var sut = new EditorSurface(fs);
        sut.LoadFile("/blog/post.md");

        TypeText(sut, "changed content");
        sut.Save();

        fs.File.ReadAllText("/blog/post.md").Should().Be("changed content");
    }

    [Fact]
    public void IsModified_Returns_False_After_Save()
    {
        var fs = new MockFileSystem();
        fs.AddFile("/blog/post.md", new MockFileData("original content"));
        var sut = new EditorSurface(fs);
        sut.LoadFile("/blog/post.md");

        TypeText(sut, "changed content");
        sut.IsModified.Should().BeTrue();

        sut.Save();

        sut.IsModified.Should().BeFalse();
    }

    [Fact]
    public void FileSaved_Event_Fires_With_The_File_Path_On_Save()
    {
        var fs = new MockFileSystem();
        fs.AddFile("/blog/post.md", new MockFileData("original content"));
        var sut = new EditorSurface(fs);
        sut.LoadFile("/blog/post.md");

        string? savedPath = null;
        sut.FileSaved += path => savedPath = path;

        sut.Save();

        savedPath.Should().Be("/blog/post.md");
    }

    [Fact]
    public void Save_Is_NoOp_When_No_File_Loaded()
    {
        var fs = new MockFileSystem();
        var sut = new EditorSurface(fs);

        var act = () => sut.Save();

        act.Should().NotThrow();
    }

    private static void TypeText(EditorSurface surface, string text) => surface._textView.Text = text;
}
