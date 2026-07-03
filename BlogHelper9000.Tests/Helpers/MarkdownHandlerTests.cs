using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.YamlParsing;

namespace BlogHelper9000.Tests.Helpers;

public class MarkdownHandlerTests
{
    [Fact]
    public void UpdateFile_Should_Not_Leave_StaleBytes_When_NewHeader_Is_Shorter()
    {
        var fileSystem = new MockFileSystem();
        const string original = """
                                 ---
                                 title: A very long original title that takes up plenty of space
                                 description: A long description that will make the original header huge
                                 layout: post
                                 ---
                                 Body content.
                                 """;
        fileSystem.AddFile("/post.md", new MockFileData(original));
        var handler = new MarkdownHandler(fileSystem);
        var markdownFile = handler.LoadFile("/post.md");
        markdownFile.Metadata.Description = string.Empty;
        markdownFile.Metadata.Layout = "x";

        handler.UpdateFile(markdownFile);

        var contents = fileSystem.File.ReadAllText("/post.md");
        contents.Should().NotContain("A long description");
        contents.Should().Contain("Body content.");
    }

    [Fact]
    public void UpdateFile_Should_Throw_When_File_Is_Missing_ClosingDelimiter()
    {
        var fileSystem = new MockFileSystem();
        const string original = """
                                 ---
                                 title: No closing delimiter
                                 Body content that runs straight on.
                                 """;
        fileSystem.AddFile("/post.md", new MockFileData(original));
        var handler = new MarkdownHandler(fileSystem);
        var markdownFile = new MarkdownFile("/post.md", new YamlHeader { Title = "New" });

        var act = () => handler.UpdateFile(markdownFile);

        act.Should().Throw<YamlConvertException>();
    }

    [Fact]
    public void UpdateFile_Should_Preserve_BodyText_Containing_DelimiterLookalike()
    {
        var fileSystem = new MockFileSystem();
        const string original = """
                                 ---
                                 title: Original
                                 ---
                                 Before the divider.
                                 ---
                                 After the divider.
                                 """;
        fileSystem.AddFile("/post.md", new MockFileData(original));
        var handler = new MarkdownHandler(fileSystem);
        var markdownFile = handler.LoadFile("/post.md");
        markdownFile.Metadata.Title = "Updated";

        handler.UpdateFile(markdownFile);

        var contents = fileSystem.File.ReadAllText("/post.md");
        contents.Should().Contain("Before the divider.");
        contents.Should().Contain("After the divider.");
    }

    [Fact]
    public void Deserialise_Should_Throw_When_ClosingDelimiter_Missing()
    {
        var fileSystem = new MockFileSystem();
        var yamlConvert = new YamlConvert(fileSystem);

        var act = () => yamlConvert.Deserialise(["---", "title: no closing delimiter"]);

        act.Should().Throw<YamlConvertException>();
    }

    [Fact]
    public void GetBody_Should_Return_Text_After_Delimiters_Without_Separator_Blank_Line()
    {
        var fileSystem = new MockFileSystem();
        const string original = """
                                 ---
                                 title: Original
                                 ---

                                 Line one.
                                 Line two.
                                 """;
        fileSystem.AddFile("/post.md", new MockFileData(original));
        var handler = new MarkdownHandler(fileSystem);

        var body = handler.GetBody("/post.md");

        body.Should().Be($"Line one.{Environment.NewLine}Line two.");
    }

    [Fact]
    public void GetBody_Should_Throw_When_ClosingDelimiter_Missing()
    {
        var fileSystem = new MockFileSystem();
        const string original = """
                                 ---
                                 title: No closing delimiter
                                 Body content.
                                 """;
        fileSystem.AddFile("/post.md", new MockFileData(original));
        var handler = new MarkdownHandler(fileSystem);

        var act = () => handler.GetBody("/post.md");

        act.Should().Throw<YamlConvertException>();
    }

    [Fact]
    public void UpdateFile_With_NewBody_Should_Replace_Body_And_RoundTrip_Through_GetBody()
    {
        var fileSystem = new MockFileSystem();
        const string original = """
                                 ---
                                 title: Original
                                 ---
                                 Old body.
                                 """;
        fileSystem.AddFile("/post.md", new MockFileData(original));
        var handler = new MarkdownHandler(fileSystem);
        var markdownFile = handler.LoadFile("/post.md");
        markdownFile.Metadata.Title = "Updated";

        handler.UpdateFile(markdownFile, "New body.\nSecond line.");

        var contents = fileSystem.File.ReadAllText("/post.md");
        contents.Should().NotContain("Old body.");
        handler.GetBody("/post.md").Should().Be($"New body.{Environment.NewLine}Second line.");
    }

    [Fact]
    public void AddPost_Content_Should_RoundTrip_Through_GetBody()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/post.md", new MockFileData("---\ntitle: Original\n---\n\nHello, world."));
        var handler = new MarkdownHandler(fileSystem);

        handler.GetBody("/post.md").Should().Be("Hello, world.");
    }
}
