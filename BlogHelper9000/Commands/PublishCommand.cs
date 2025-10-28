namespace BlogHelper9000.Commands;

internal sealed class PublishCommand 
{
    public PublishCommand() 
    {
        // var postArgument = new Argument<string>(
        //     name: "post",
        //     description: "The post to publish");
        // AddArgument(postArgument);
        //
        // this.SetHandler((post, fileSystem, baseDirectory, logger) =>
        // {
        //     logger.LogTrace("{Command}.SetHandler", nameof(PublishCommand));
        //     var handler = new PublishCommandHandler(logger, new PostManager(fileSystem, baseDirectory));
        //     logger.LogDebug("Executing {CommandHandler} from {Command}", nameof(PublishCommandHandler),
        //         nameof(PublishCommand));
        //     handler.Execute(post);
        // }, postArgument, new FileSystemBinder(), new BaseDirectoryBinder(), new LoggingBinder());
    }
}