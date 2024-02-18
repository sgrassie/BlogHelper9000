using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.Commands;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Commands;

public class PublishCommandTests : CommandTestsBase
{
    [Fact]
    public async Task Should_Output_Help()
    {
        var console = new TestConsole();
        var option = BasePathOption();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var command = new PublishCommand(fileSystem, option);
        command.AddOption(option);

        await command.InvokeAsync("publish -h", console);

        var lines = console.AsLines();

        lines.Should().Contain(x => x.StartsWith("Description:"));
        lines.Should().Contain(x => x.StartsWith("Arguments:"));
        lines.Should().Contain(x => x.StartsWith("<post> "));
    }
}