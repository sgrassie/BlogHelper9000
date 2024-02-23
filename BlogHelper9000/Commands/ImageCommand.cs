using System.CommandLine;
using BlogHelper9000.Commands.Binders;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

internal sealed class ImageCommand : Command
{
    public ImageCommand(IFileSystem fileSystem) 
        : base("image", "Manipulate images in blog posts")
    {
        AddCommand(new AddSubCommand(fileSystem));
        AddCommand(new UpdateSubCommand(fileSystem));
    }

    private static class ImageCommandSharedOptions
    {
        public static readonly Argument<string> PostArg = new(
            "post", "The post to update the image for.");

        public static readonly Argument<string> QueryArg = new(
            "query", () => "programming", "The image query."
        );

        public static readonly Option<string> AuthorBrandingOption = new(
            new []{"--branding", "-b"}, description: "Optionally provide author branding.",
            getDefaultValue: () => "/assets/images/branding_logo.png"
        );
    }
    
    private class AddSubCommand : Command
    {
        public AddSubCommand(IFileSystem fileSystem) 
            : base("add", "Add an image to a post")
        {
            AddArgument(ImageCommandSharedOptions.PostArg);
            AddArgument(ImageCommandSharedOptions.QueryArg);
        }
    }

    private class UpdateSubCommand : Command
    {
        public UpdateSubCommand(IFileSystem fileSystem) 
            : base("update", "Update images in posts")
        {
            AddCommand(new UpdatePostSubCommand(fileSystem));
            AddCommand(new UpdateAllSubCommand(fileSystem));
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