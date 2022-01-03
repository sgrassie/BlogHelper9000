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
            FixDescription(file);
            FixTags(file);

            MarkdownHandler.UpdateFile(file);
        }

        return true;
    }

    private void FixTags(MarkdownFile file)
    {
        ConsoleWriter.WriteWithIndent(ConsoleColor.White, 5, "Fixing Tags");
        if (file.Metadata.Extras.TryGetValue("category", out var category))
        {
            ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Converting category to tags");
            file.Metadata.Tags = category.Contains(',')
                ? SplitToQuotedList(category)
                : new List<string> { $"'{category}'" };
        }

        if (file.Metadata.Extras.TryGetValue("categories", out var categories))
        {
            ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Converting categories to tags");
            file.Metadata.Tags = SplitToQuotedList(categories.Replace("[", string.Empty).Replace("]", string.Empty));
        }

        List<string> SplitToQuotedList(string s)
        {
            return s
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => $"'{s}'")
                .ToList();
        }
    }

    private void FixDescription(MarkdownFile file)
    {
        ConsoleWriter.WriteWithIndent(ConsoleColor.White, 5, "Check description is updated");
        // some old posts have a metadescription tag
        if (file.Metadata.Extras.TryGetValue("metadescription", out var metadescription))
        {
            ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Updating Description");
            file.Metadata.Description = metadescription;
        }
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