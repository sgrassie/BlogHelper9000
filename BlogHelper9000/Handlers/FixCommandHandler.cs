using System.CommandLine;
using System.Globalization;
using System.IO.Abstractions;
using BlogHelper9000.Helpers;

namespace BlogHelper9000.Handlers;

public class FixCommandHandler
{
    private readonly FileSystemHelper _fileSystemHelper;
    public FixCommandHandler(IFileSystem fileSystem, string baseDirectory, IConsole console)
    {
        _fileSystemHelper = new FileSystemHelper(fileSystem, baseDirectory);
    }

    public void Execute(bool status, bool description, bool tags, bool series)
    {
        foreach (var file in _fileSystemHelper.FileSystem.Directory.EnumerateFiles(_fileSystemHelper.Posts, "*.md", SearchOption.AllDirectories)
                     .Select(_fileSystemHelper.Markdown.LoadFile))
        {
            //ConsoleWriter.Write("Updating metadata for {0}", file.Metadata.Title);

            FixPublishedStatus(file);
            // FixDescription(file);
            // FixTags(file);
            // UpdateIsSeries(file);

            _fileSystemHelper.Markdown.UpdateFile(file);
        }
    }
    
    private void FixPublishedStatus(MarkdownFile file)
    {
        var dateFromFileName = ExtractPublishedDateFromFileName(file.FilePath);
        
        if (file.Metadata.PublishedOn is null)
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 5, "Updating published status");
            if (file.Metadata.PublishedOn is null)
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Updating Published date");
                file.Metadata.PublishedOn = dateFromFileName;
            }

            if (file.Metadata.PublishedOn is not null)
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Updating IsPublished");
                file.Metadata.IsPublished = true;
                file.Metadata.IsHidden = false;
            }
        }
        else
        {
            // some of the posts have the incorrect published date in the header
            file.Metadata.PublishedOn = dateFromFileName;
            file.Metadata.IsPublished = true;
            file.Metadata.IsHidden = false;
        }

        DateTime ExtractPublishedDateFromFileName(string fileName)
        {
            var rawFileName = fileName.Split("/").Last();
            var datePart = rawFileName.Substring(0, 10);
            return DateTime.ParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;
        }
    }

}