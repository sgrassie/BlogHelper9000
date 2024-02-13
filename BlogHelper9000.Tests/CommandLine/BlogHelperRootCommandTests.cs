using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.CommandLine;

namespace BlogHelper9000.Tests.CommandLine;

public class BlogHelperRootCommandTests
{
    [Fact]
    public async Task Should_Output_Help_WhenInvokingHelp()
    {
        var console = new TestConsole();
        var rootCommand = new BlogHelperRootCommand(JekyllBlogFilesystemFactory.FileSystem);

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
        var rootCommand = new BlogHelperRootCommand(JekyllBlogFilesystemFactory.FileSystem);

        await rootCommand.InvokeAsync("-h", console);

        console.Out.ToString()
            .Should().Contain(expectedCommand);
    }
}