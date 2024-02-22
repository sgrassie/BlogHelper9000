using BlogHelper9000.Commands.Binders;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

internal sealed class PublishCommand : Command
{
    public PublishCommand(IFileSystem fileSystem) : base("publish", "Publishes a blog post")
    {
        var postArgument = new Argument<string>(
            name: "post",
            description: "The post to publish");
        AddArgument(postArgument);

        this.SetHandler((post, baseDirectory, logger) =>
        {
            logger.LogTrace("{Command}.SetHandler", nameof(PublishCommand));
            var handler = new PublishCommandHandler(logger, new PostManager(fileSystem, baseDirectory));
            logger.LogDebug("Executing {CommandHandler} from {Command}", nameof(PublishCommandHandler),
                nameof(PublishCommand));
            handler.Execute(post);
        }, postArgument, GlobalOptions.BaseDirectoryOption, new LoggingBinder());
    }
}