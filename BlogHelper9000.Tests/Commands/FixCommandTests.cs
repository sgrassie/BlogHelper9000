using BlogHelper9000.Commands;

namespace BlogHelper9000.Tests.Commands;

public class FixCommandTests 
{
    [Fact]
    public async Task Should_Output_Help()
    {
        // var console = new TestConsole();
        // var command = new FixCommand();
        // await command.InvokeAsync("fix -h", console);
        //
        // console.Out.ToString()
        //     .Should().Contain("fix [options]");
    }
    
    [Theory]
    [InlineData("-s, --status", "Fix the published status of a post")]
    [InlineData("-d, --description", "Fix the description of a post")]
    [InlineData("-t, --tags", "Fix the tags of a post")]
    public async Task Should_Output_Options(string optionName, string optionHelp)
    {
        // var console = new TestConsole();
        // var command = new FixCommand();
        // await command.InvokeAsync("fix -h", console);
        //
        // var lines = console.AsLines()
        //     .Where(line => line.StartsWith("-")).ToList();
        //
        // lines.Should().Contain(x => x.StartsWith(optionName) && x.Contains(optionHelp));
    }
}