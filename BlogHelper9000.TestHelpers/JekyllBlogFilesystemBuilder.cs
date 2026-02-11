using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace BlogHelper9000.TestHelpers;

public sealed class JekyllBlogFilesystemBuilder
{
    private const string _drafts = @"/blog/_drafts";
    private const string _posts = @"/blog/_posts";

    private readonly MockFileSystem _fileSystem;

    public JekyllBlogFilesystemBuilder()
    {
        _fileSystem = new MockFileSystem();
        _fileSystem.AddDirectory(_posts);
        _fileSystem.AddDirectory(_drafts);
    }

    public static string Drafts => _drafts;
    public static string Posts => _posts;

    public JekyllBlogFilesystemBuilder AddFile(string path, MockFileData mockData)
    {
        _fileSystem.AddFile(path, mockData);
        return this;
    }

    public JekyllBlogFilesystemBuilder AddFiles(Dictionary<string, MockFileData> files)
    {
        foreach (var (file, data) in files)
        {
            _fileSystem.AddFile(file, data);
        }

        return this;
    }

    public IFileSystem BuildFileSystem()
    {
        return _fileSystem;
    }
}
