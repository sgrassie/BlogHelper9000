using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.Commands;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Commands;

public class ImageCommandTests
{
    [Fact]
    public async Task Should_Output_Help()
    {
        var expectedHelp = new List<string>
        {
            "Description:",
            "Manipulate images in blog posts",
            "Usage:",
            "image [command] [options]",
            "Options:",
            "-b, --base-directory <base-directory>  The base directory of the blog.",
            "--version                              Show version information",
            "-?, -h, --help                         Show help and usage information",
            "Commands:",
            "add <post>  Add an image to a post",
        };
        var console = new TestConsole();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var command = new ImageCommand(fileSystem);
        command.AddOption(GlobalOptions.BaseDirectoryOption);

        await command.InvokeAsync("image -h", console);

        var lines = console.AsLines();

        lines.Should().ContainInOrder(expectedHelp);
    }
}