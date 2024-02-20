using BlogHelper9000.Commands.Binders;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Reporters;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

public class InfoCommand : Command
{
    public InfoCommand(IFileSystem fileSystem) 
        : base("info", "Useful information and metrics about the blog")
    {
        this.SetHandler((baseDirectory, console) =>
        {
            var handler = new InfoCommandHandler(new PostManager(fileSystem, baseDirectory), console);
            handler.Execute(new InfoCommandReporter());
        }, GlobalOptions.BaseDirectoryOption, Bind.FromServiceProvider<IConsole>());
    }
}