namespace BlogHelper9000.Commands;

public static class GlobalOptions
{
    public static readonly Option<string> BaseDirectoryOption = new(
            new[] {"--base-directory", "-b"},
            description: "The base directory of the blog.");

    public static readonly Option<LogLevel> VerbosityOption = new(
        new[] { "--verbosity", "-v" },
        description: "The log level to output.",
        getDefaultValue: () => LogLevel.None);
}