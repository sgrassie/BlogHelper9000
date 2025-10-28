namespace BlogHelper9000.Commands;

internal sealed class AddCommand 
{
    public AddCommand()
    {
        // var titleArgument = new Argument<string>("title", "The title of the new blog post");
        // var tagsArgument = new Argument<string[]>("tags", "The tags for the new blog post");
        // AddArgument(titleArgument);
        // AddArgument(tagsArgument);
        //
        // var draftOption = new Option<bool>(
        //     name: "--draft",
        //     description: "Adds the post as a draft");
        //
        // var isFeaturedOption = new Option<bool>(
        //     name: "--is-featured",
        //     description: "Sets the post as a featured post");
        //
        // var isHidden = new Option<bool>(
        //     name: "--is-hidden",
        //     description: "Sets whether the post is hidden or not");
        //
        // var featuredImageOption = new Option<string>(
        //     name: "--featured-image",
        //     description: "Sets the featured image path");
        //
        // AddOption(draftOption);
        // AddOption(isFeaturedOption);
        // AddOption(isHidden);
        // AddOption(featuredImageOption);
        //
        // this.SetHandler((options, fileSystem, blogBaseDir, logger) =>
        //     {
        //         logger.LogTrace("{Command}.SetHandler", nameof(AddCommand));
        //         var handler = new AddCommandHandler(logger, new PostManager(fileSystem, blogBaseDir));
        //         logger.LogDebug("Executing {CommandHandler} from {Command}", nameof(AddCommandHandler), nameof(AddCommand));
        //         handler.Execute(options);
        //     },
        //     new AddCommandOptionsBinder(
        //     titleArgument, 
        //     tagsArgument, 
        //     draftOption, 
        //     featuredImageOption, 
        //     isFeaturedOption, 
        //     isHidden),
        //     new FileSystemBinder(),
        //     new BaseDirectoryBinder(),
        //     new LoggingBinder());
    }
}