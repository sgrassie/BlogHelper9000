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
    }

    private static class ImageCommandSharedOptions
    {
        public static Argument<string> PostArg = new(
            "post", "The post to update the image for.");
    }
    
    private class AddSubCommand : Command
    {
        public AddSubCommand(IFileSystem fileSystem) : base("add", "Add an image to a post")
        {
            AddArgument(ImageCommandSharedOptions.PostArg);
        }
    }
}