
namespace BlogHelper9000.Commands;

internal sealed class ImageCommand 
{
    
    public ImageCommand() 
    {
        // AddCommand(new AddSubCommand());
        // AddCommand(new UpdateSubCommand());
        // AddCommand(new UnsplashCredentialsCommand());
    }

    // private static class ImageCommandSharedOptions
    // {
    //     public static readonly Argument<string> PostArg = new(
    //         "post", "The post to update the image for.");
    //
    //     public static readonly Argument<string> QueryArg = new(
    //         "query", () => "programming", "The image query."
    //     );
    //
    //     public static readonly Option<string> AuthorBrandingOption = new(
    //         new []{"--branding"}, description: "Optionally provide author branding."
    //     );
    // }
    //
    //
    //
    // private class UpdateSubCommand : Command
    // {
    //     public UpdateSubCommand() 
    //         : base("update", "Update images in posts")
    //     {
    //         // AddCommand(new UpdatePostSubCommand(fileSystem));
    //         // AddCommand(new UpdateAllSubCommand(fileSystem));
    //         AddGlobalOption(ImageCommandSharedOptions.AuthorBrandingOption);
    //     }
    //
    //     private class UpdatePostSubCommand : Command
    //     {
    //         public UpdatePostSubCommand(IFileSystem fileSystem)
    //             : base("post", "Updates the image in a specific post")
    //         {
    //             AddArgument(ImageCommandSharedOptions.PostArg);
    //             AddArgument(ImageCommandSharedOptions.QueryArg);
    //         }
    //     }
    //
    //     private class UpdateAllSubCommand : Command
    //     {
    //         public UpdateAllSubCommand(IFileSystem fileSystem)
    //             : base("all", "Updates all images in all posts")
    //         {
    //             AddArgument(ImageCommandSharedOptions.QueryArg);
    //         }
    //     }
    // }
}