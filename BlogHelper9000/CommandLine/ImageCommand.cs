using System.CommandLine;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.CommandLine;

internal sealed class ImageCommand : Command
{
    public ImageCommand() 
        : base("image", "Manipulate images in blog posts")
    {
        AddArgument(new Argument<string>("post", "The post to update the image for."));
    }
}