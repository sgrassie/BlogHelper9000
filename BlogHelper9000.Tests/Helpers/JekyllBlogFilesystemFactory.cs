using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace BlogHelper9000.Tests.Helpers;

internal sealed class JekyllBlogFilesystemFactory
{
    private const string _drafts = @"/blog/_drafts";
    private const string _posts = @"/blog/_posts";
    private static readonly Lazy<IFileSystem> FileSystemHolder = new(BuildFileSystem);

    public static IFileSystem FileSystem = FileSystemHolder.Value;

    public static string Drafts => _drafts;
    public static string Posts => _posts;

    private static IFileSystem BuildFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(_drafts);
        fileSystem.AddDirectory(_posts);

        return fileSystem;
    }
}