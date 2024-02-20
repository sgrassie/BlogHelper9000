using BlogHelper9000.Commands.Binders;
using BlogHelper9000.Handlers;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

public class PublishCommand : Command
{
    public PublishCommand(IFileSystem fileSystem) : base("publish", "Publishes a blog post")
    {
        var postArgument = new Argument<string>(
            name: "post",
            description: "The post to publish");
        AddArgument(postArgument);
        
        this.SetHandler((post, baseDirectory, console) =>
        {
            var handler = new PublishCommandHandler(fileSystem, baseDirectory, console);
            handler.Execute(post);
        }, postArgument, GlobalOptions.BaseDirectoryOption, Bind.FromServiceProvider<IConsole>());
    }
}