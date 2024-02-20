namespace BlogHelper9000.Commands;

public static class GlobalOptions
{
    public static readonly Option<string> BaseDirectoryOption = new(
            new[] {"--base-directory", "-b"},
            description: "The base directory of the blog.");
}