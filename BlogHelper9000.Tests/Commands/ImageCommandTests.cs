using BlogHelper9000.Commands;

namespace BlogHelper9000.Tests.Commands;

public class ImageCommandTests
{
    [Fact]
    public async Task Should_Output_Help_ForMainImageCommand()
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
            "add <post> <query>  Add an image to a post [default: programming]",
            "update              Update images in posts"
        };
        // var console = new TestConsole();
        // var command = new ImageCommand();
        // command.AddOption(GlobalOptions.BaseDirectoryOption);
        //
        // await command.InvokeAsync("image -h", console);
        //
        // var lines = console.AsLines();
        //
        // lines.Should().ContainInOrder(expectedHelp);
    }

    [Fact(Skip = "Not including the commands for this just now")]
    public async Task Should_Output_Help_ForImageUpdateSubCommand()
    {
        var expectedHelp = new[]
        {
            "Description:", 
            "Update images in posts", 
            "Usage:", 
            "image update [command] [options]", 
            "Options:", 
            "-b, --branding <branding>  Optionally provide author branding. [default: /assets/images/branding_logo.png]",
            "-?, -h, --help             Show help and usage information", 
            "Commands:", 
            "post <post> <query>  Updates the image in a specific post [default: programming]",
            "all <query>          Updates all images in all posts [default: programming]"
        };
        // var console = new TestConsole();
        // var command = new ImageCommand();
        // command.AddOption(GlobalOptions.BaseDirectoryOption);
        //
        // await command.InvokeAsync("image update -h", console);
        //
        // var lines = console.AsLines();
        //
        // lines.Should().ContainInOrder(expectedHelp);
    }
    
}