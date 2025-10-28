namespace BlogHelper9000.Commands;

public class InfoCommand 
{
    public InfoCommand() 
    {
        // this.SetHandler((fileSystem, baseDirectory, logger) =>
        // {
        //     logger.LogTrace("{Command}.SetHandler", nameof(InfoCommand));
        //     var handler = new InfoCommandHandler(logger, new PostManager(fileSystem, baseDirectory));
        //     logger.LogDebug("Executing {CommandHandler} from {Command}", nameof(InfoCommandHandler), nameof(InfoCommand));
        //     handler.Execute(new InfoCommandReporter());
        // }, new FileSystemBinder(), new BaseDirectoryBinder(), new LoggingBinder());
    }
}