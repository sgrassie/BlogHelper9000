using System.Globalization;
using BlogHelper9000.Helpers;

namespace BlogHelper9000.Handlers;

public class FixCommandHandler(ILogger logger, PostManager postManager)
{
    public void Execute(bool status, bool description, bool tags)
    {
        foreach (var file in postManager.GetAllPosts())
        {
            logger.LogInformation("Updating metadata for {PostTitle}", file.Metadata.Title);

            if(status) FixPublishedStatus(file);
            if(description) FixDescription(file);
            if(tags) FixTags(file);

            postManager.Markdown.UpdateFile(file);
        }
    }
    
    private void FixPublishedStatus(MarkdownFile file)
    {
        var dateFromFileName = ExtractPublishedDateFromFileName(file.FilePath);
        file.Metadata.PublishedOn = dateFromFileName;
        file.Metadata.IsPublished = true;
        file.Metadata.IsHidden = false;

        DateTime ExtractPublishedDateFromFileName(string fileName)
        {
            var rawFileName = fileName.Split("/").Last();
            var datePart = rawFileName.Substring(0, 10);
            return DateTime.ParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;
        }
    }
    private void FixDescription(MarkdownFile file)
    {
        //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 5, "Check description is updated");
        // some old posts have a metadescription tag
        if (file.Metadata.Extras.TryGetValue("metadescription", out var metadescription))
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Updating Description");
            file.Metadata.Description = metadescription;
        }
    }

    private static void FixTags(MarkdownFile file)
    {
        //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 5, "Fixing Tags");
        if (file.Metadata.Extras.TryGetValue("category", out var category))
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Converting category to tags");
            file.Metadata.Tags = category.Contains(',')
                ? SplitToQuotedList(category)
                : [$"'{category}'"];
        }

        if (file.Metadata.Extras.TryGetValue("categories", out var categories))
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.White, 10, "Converting categories to tags");
            file.Metadata.Tags = SplitToQuotedList(categories.Replace("[", string.Empty).Replace("]", string.Empty));
        }

        // Remove any duplicate tags and make them Title Case
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        file.Metadata.Tags = file.Metadata.Tags
            .GroupBy(x => x)
            .Select(x => textInfo.ToTitleCase(x.First()))
            .ToList();

        List<string> SplitToQuotedList(string s)
        {
            return s
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => $"'{s}'")
                .ToList();
        }
    }
}