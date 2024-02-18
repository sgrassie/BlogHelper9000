using BlogHelper9000.Helpers;

namespace BlogHelper9000.Handlers;

public class PublishCommandHandler
{
    private readonly IFileSystem _fileSystem;
    private readonly string _baseDirectory;
    private readonly IConsole _console;

    private readonly FileSystemHelper _fileSystemHelper;

    public PublishCommandHandler(IFileSystem fileSystem, string baseDirectory, IConsole console)
    {
        _fileSystem = fileSystem;
        _baseDirectory = baseDirectory;
        _console = console;

        _fileSystemHelper = new FileSystemHelper(fileSystem, baseDirectory);
    }

    public void Execute(string post)
    {
        if (TryFindPublishablePost(post, out var postMarkdown))
        {
            if (postMarkdown != null)
            {
                var currentPath = postMarkdown.FilePath;
                postMarkdown.Metadata.IsPublished = true;
                postMarkdown.Metadata.PublishedOn = DateTime.Now;
                _fileSystemHelper.Markdown.UpdateFile(postMarkdown);

                var publishedFilename = $"{DateTime.Now:yyyy-MM-dd}-{post}";
                var targetFolder = _fileSystemHelper.FileSystem.Path.Combine(_fileSystemHelper.Posts, $"{DateTime.Now:yyyy}");

                if (!_fileSystemHelper.FileSystem.Directory.Exists(targetFolder)) _fileSystemHelper.FileSystem.Directory.CreateDirectory(targetFolder);
                var replacementPath = _fileSystemHelper.FileSystem.Path.Combine(targetFolder, publishedFilename);
                //ConsoleWriter.Write("Publishing {0} to {1}", publishedFilename, targetFolder);
                _fileSystemHelper.FileSystem.File.Move(currentPath, replacementPath);
                _fileSystemHelper.FileSystem.File.Delete(currentPath);
                //await Command.RunAsync("git", "add --all", input.BaseDirectoryFlag, true);
                //ConsoleWriter.Write("Published file added to git index. Don't forget to commit and push to remote.");
            }
        }
        else
        {
            _console.WriteLine("Could not find specified post to publish");
        }
    }

    private bool TryFindPublishablePost(string title, out MarkdownFile? markdownFile)
    {
        var potentialDraftPath = _fileSystemHelper.CreateDraftPath(title);
        var potentialPostsPath = _fileSystemHelper.CreatePostPath(title);
        
        if (_fileSystemHelper.FileSystem.File.Exists(potentialDraftPath))
        {
            markdownFile = _fileSystemHelper.Markdown.LoadFile(potentialDraftPath);
            return true;
        }

        if (_fileSystemHelper.FileSystem.File.Exists(potentialPostsPath))
        {
            markdownFile = _fileSystemHelper.Markdown.LoadFile(potentialPostsPath);
            return true;
        }

        markdownFile = null;
        return false;
    }
}