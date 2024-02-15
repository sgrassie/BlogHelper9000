using BlogHelper9000.Handlers;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

public class InfoCommand : Command
{
    public InfoCommand(IFileSystem fileSystem, Option<string> baseDirectoryOption) 
        : base("info", "Useful information and metrics about the blog")
    {
        this.SetHandler((baseDirectory, console) =>
        {
            var handler = new InfoCommandHandler(fileSystem, baseDirectory, console);
            handler.Execute();
        }, baseDirectoryOption, Bind.FromServiceProvider<IConsole>());
    }
}