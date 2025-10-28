using BlogHelper9000.Commands;

namespace BlogHelper9000.Tests.Commands;

public class PublishCommandTests 
{
    [Fact]
    public async Task Should_Output_Help()
    {
        // var console = new TestConsole();
        // var command = new PublishCommand();
        //
        // await command.InvokeAsync("publish -h", console);
        //
        // var lines = console.AsLines().ToList();
        //
        // lines.Should().Contain(x => x.StartsWith("Description:"));
        // lines.Should().Contain(x => x.StartsWith("Arguments:"));
        // lines.Should().Contain(x => x.StartsWith("<post> "));
    }
}