using Command = System.CommandLine.Command;

namespace BlogHelper9000.CommandLine;

public class FixCommand : Command
{
    public FixCommand() : base("fix", "Fixes various things about the blog posts")
    {
        
    }
}