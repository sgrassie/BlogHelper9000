using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.Commands;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Commands;

public class BlogHelperRootCommandTests
{
    [Fact]
    public async Task Should_Output_Help_WhenInvokingHelp()
    {
        var console = new TestConsole();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var rootCommand = new BlogHelperRootCommand(fileSystem);

        await rootCommand.InvokeAsync("-h", console);

        console.Out.ToString()
            .Should().Contain("help");
    }

    [Theory]
    [InlineData("add")]
    [InlineData("image")]
    [InlineData("info")]
    [InlineData("publish")]
    [InlineData("fix")]
    public async Task Should_Output_Commands_WhenInvokingHelp(string expectedCommand)
    {
        var console = new TestConsole();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var rootCommand = new BlogHelperRootCommand(fileSystem);

        await rootCommand.InvokeAsync("-h", console);

        console.Out.ToString()
            .Should().Contain(expectedCommand);
    }
}