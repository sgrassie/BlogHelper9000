using BlogHelper9000.Commands.Binders;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Imager;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

internal sealed class ImageCommand : Command
{
    public ImageCommand() 
        : base("image", "Manipulate images in blog posts")
    {
        AddCommand(new AddSubCommand());
        AddCommand(new UpdateSubCommand());
    }

    private static class ImageCommandSharedOptions
    {
        public static readonly Argument<string> PostArg = new(
            "post", "The post to update the image for.");

        public static readonly Argument<string> QueryArg = new(
            "query", () => "programming", "The image query."
        );

        public static readonly Option<string> AuthorBrandingOption = new(
            new []{"--branding"}, description: "Optionally provide author branding."
        );
    }
    
    private class AddSubCommand : Command
    {
        public AddSubCommand() 
            : base("add", "Add an image to a post")
        {
            AddArgument(ImageCommandSharedOptions.PostArg);
            AddArgument(ImageCommandSharedOptions.QueryArg);
            
            this.SetHandler(async (post, query, branding, fileSystem, logger, baseDirectory) =>
                {
                    var postManager = new PostManager(fileSystem, baseDirectory);
                    var handler = new ImageCommandAddSubCommandHandler(logger, postManager, new UnsplashClient(logger), new ImageProcessor(logger, postManager));
                    await handler.Execute(post, query, branding);
                }, 
                ImageCommandSharedOptions.PostArg,
                ImageCommandSharedOptions.QueryArg,
                ImageCommandSharedOptions.AuthorBrandingOption,
                new FileSystemBinder(),
                new LoggingBinder(),
                new BaseDirectoryBinder());
        }
    }

    private class UpdateSubCommand : Command
    {
        public UpdateSubCommand() 
            : base("update", "Update images in posts")
        {
            // AddCommand(new UpdatePostSubCommand(fileSystem));
            // AddCommand(new UpdateAllSubCommand(fileSystem));
            AddGlobalOption(ImageCommandSharedOptions.AuthorBrandingOption);
        }

        private class UpdatePostSubCommand : Command
        {
            public UpdatePostSubCommand(IFileSystem fileSystem)
                : base("post", "Updates the image in a specific post")
            {
                AddArgument(ImageCommandSharedOptions.PostArg);
                AddArgument(ImageCommandSharedOptions.QueryArg);
            }
        }

        private class UpdateAllSubCommand : Command
        {
            public UpdateAllSubCommand(IFileSystem fileSystem)
                : base("all", "Updates all images in all posts")
            {
                AddArgument(ImageCommandSharedOptions.QueryArg);
            }
        }
    }
}