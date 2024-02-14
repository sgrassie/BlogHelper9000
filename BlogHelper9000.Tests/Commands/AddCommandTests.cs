using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.Commands;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Commands;

public class AddCommandTests : CommandTestsBase
{
    [Fact]
    public async Task Should_Output_Help()
    {
        var console = new TestConsole();
        var option = BasePathOption();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var command = new AddCommand(fileSystem, option);
        command.AddOption(option);
        
        await command.InvokeAsync("add -h", console);
        
       console.Out.ToString() 
            .Should().Contain("add <title> [<tags>...] [options]");
    }
    
    [Theory]
    [InlineData("--draft", "Adds the post as a draft")]
    [InlineData("--is-featured", "Sets the post as a featured post")]
    [InlineData("--is-hidden", "Sets whether the post is hidden or not")]
    [InlineData("--featured-image", "Sets the featured image path")]
    [InlineData("--version", "Show version information")]
    public async Task Should_Output_Options(string optionName, string optionHelp)
    {
        var console = new TestConsole();
        var option = BasePathOption();
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var command = new AddCommand(fileSystem, option);
        command.AddOption(option);
        await command.InvokeAsync("add -h", console);

        var lines = console.Out.ToString()
            .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("--")).ToList();

        lines.Should().Contain(x => x.StartsWith(optionName) && x.Contains(optionHelp));
    }
}