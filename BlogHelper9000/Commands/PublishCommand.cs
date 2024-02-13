using BlogHelper9000.Commands.Inputs;

namespace BlogHelper9000.Commands;

public class PublishCommand : AsyncBaseCommand<PublishInput>
{
    public PublishCommand()
    {
        Usage("Publish a post!").Arguments(x => x.Post);
    }

    protected override async Task<bool> Run(PublishInput input)
    {
        var draft = Path.Combine(DraftsPath, input.Post);
        var markdownFile = MarkdownHandler.LoadFile(draft);
        markdownFile.Metadata.IsPublished = true;
        markdownFile.Metadata.PublishedOn = DateTime.Now;
        MarkdownHandler.UpdateFile(markdownFile);
        
        var publishedFilename = $"{DateTime.Now:yyyy-MM-dd}-{input.Post}";
        var targetFolder = Path.Combine(PostsPath, $"{DateTime.Now:yyyy}");

        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
        var replacementPath = Path.Combine(targetFolder, publishedFilename);
        //ConsoleWriter.Write("Publishing {0} to {1}", publishedFilename, targetFolder);
        File.Move(draft, replacementPath);
        File.Delete(draft);
        await Command.RunAsync("git", "add --all", input.BaseDirectoryFlag, true);
        //ConsoleWriter.Write("Published file added to git index. Don't forget to commit and push to remote.");
        return true;
    }

    protected override bool ValidateInput(PublishInput input)
    {
        if (base.ValidateInput(input))
        {
            if (!input.Post.EndsWith(".md"))
            {
                //ConsoleWriter.Write(ConsoleColor.Red, "You must specify the post file to publish");
                return false;
            }

            if (!File.Exists(Path.Combine(DraftsPath, input.Post)))
            {
                //ConsoleWriter.Write(ConsoleColor.Red, "You must specify the post file to publish");
                return false;
            }

            return true;
        }

        return false;
    }
}