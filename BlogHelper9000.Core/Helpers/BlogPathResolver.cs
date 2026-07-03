using System.IO.Abstractions;

namespace BlogHelper9000.Core.Helpers;

public class BlogPathResolver(IFileSystem fileSystem, string baseDirectory)
{
    private readonly string _normalizedBase = fileSystem.Path.GetFullPath(baseDirectory);

    public bool TryResolveWithinBase(string candidatePath, out string resolvedPath)
    {
        var fullBase = _normalizedBase;
        var candidate = fileSystem.Path.IsPathRooted(candidatePath)
            ? candidatePath
            : fileSystem.Path.Combine(fullBase, candidatePath);

        var fullCandidate = fileSystem.Path.GetFullPath(candidate);

        var baseWithSeparator = fullBase.EndsWith(fileSystem.Path.DirectorySeparatorChar)
            ? fullBase
            : fullBase + fileSystem.Path.DirectorySeparatorChar;

        if (fullCandidate.Equals(fullBase, StringComparison.Ordinal) ||
            fullCandidate.StartsWith(baseWithSeparator, StringComparison.Ordinal))
        {
            resolvedPath = fullCandidate;
            return true;
        }

        resolvedPath = string.Empty;
        return false;
    }
}
