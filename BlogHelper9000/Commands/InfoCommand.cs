using BlogHelper9000.Commands.Binders;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Reporters;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

public class InfoCommand : Command
{
    public InfoCommand() 
        : base("info", "Useful information and metrics about the blog")
    {
        this.SetHandler((fileSystem, baseDirectory, logger) =>
        {
            logger.LogTrace("{Command}.SetHandler", nameof(InfoCommand));
            var handler = new InfoCommandHandler(logger, new PostManager(fileSystem, baseDirectory));
            logger.LogDebug("Executing {CommandHandler} from {Command}", nameof(InfoCommandHandler), nameof(InfoCommand));
            handler.Execute(new InfoCommandReporter());
        }, new FileSystemBinder(), new BaseDirectoryBinder(), new LoggingBinder());
    }
}