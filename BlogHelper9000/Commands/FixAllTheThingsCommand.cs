using System.Globalization;

namespace BlogHelper9000.Commands;

public class FixAllTheThingsCommand : BaseCommand<BaseInput>
{
    public FixAllTheThingsCommand()
    {
        Usage("Fix all of the things");
    }
    
    protected override bool Run(BaseInput input)
    {
        foreach (var file in Directory.EnumerateFiles(PostsPath, "*.md", SearchOption.AllDirectories)
                     .Select(MarkdownHandler.LoadFile))
        {
            ConsoleWriter.Write("Updating metadata for {0}", file.Metadata.Title);
            
            FixPublishedStatus(file);
            

            MarkdownHandler.UpdateFile(file);
        }

        return true;
    }

    private void FixPublishedStatus(MarkdownFile file)
    {
        if (file.Metadata.PublishedOn is null)
        {
            ConsoleWriter.WriteWithIndent(ConsoleColor.White, 5, "Updating published status");
            if (file.Metadata.PublishedOn is null)
            {
                ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Updating Published date");
                file.Metadata.PublishedOn = ExtractPublishedDateFromFileName(file.FilePath);
            }

            if (file.Metadata.PublishedOn is not null)
            {
                ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Updating IsPublished");
                file.Metadata.IsPublished = true;
            }

            DateTime ExtractPublishedDateFromFileName(string fileName)
            {
                var rawFileName = fileName.Split("/").Last();
                var datePart = rawFileName.Substring(0, 10);
                return DateTime.ParseExact(datePart, "yyyy-mm-dd", CultureInfo.InvariantCulture).Date;
            }
        }
    }
}