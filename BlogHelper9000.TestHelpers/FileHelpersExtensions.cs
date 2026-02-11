using System.IO.Abstractions;

namespace BlogHelper9000.TestHelpers;

public static class FileHelpersExtensions
{
    /// <summary>
    /// Get the file contents of the specified <param name="fileName" /> as a string[].
    /// </summary>
    /// <param name="fileSystem">The filesystem.</param>
    /// <param name="fileName">The filename.</param>
    /// <returns>The file contents as a string[]</returns>
    public static string[] FileContentsAsArray(this IFileSystem fileSystem, string fileName)
    {
        var contents = fileSystem
            .File
            .ReadAllText(fileName);
        return contents
            .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }
}
