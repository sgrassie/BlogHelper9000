using Command = System.CommandLine.Command;

namespace BlogHelper9000.CommandLine;

public class PublishCommand : Command
{
    public PublishCommand() : base("publish", "Publishes a blog post")
    {
        
    }
}