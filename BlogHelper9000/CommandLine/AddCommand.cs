using System.CommandLine;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.CommandLine;

internal sealed class AddCommand : Command
{
    public AddCommand(Option<string> baseDirectory) 
        : base("add", "Adds a new blog post")
    {
        var addArgument = new Argument<string>("title", "The title of the new blog post");
        AddArgument(addArgument);

        var draftOption = new Option<bool>(
            name: "--draft",
            description: "Adds the post as a draft");
        AddOption(draftOption);
        
        this.SetHandler((title, draft, console) =>
        {
            console.WriteLine(title);
        }, addArgument, draftOption, Bind.FromServiceProvider<IConsole>());
    }
}