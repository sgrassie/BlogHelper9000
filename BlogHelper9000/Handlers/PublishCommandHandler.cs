using System.Diagnostics.CodeAnalysis;
using BlogHelper9000.Helpers;

namespace BlogHelper9000.Handlers;

public class PublishCommandHandler(ILogger logger, PostManager postManager)
{
    public void Execute(string post)
    {
        if (postManager.TryFindPost(post, out var postMarkdown))
        {
            logger.LogDebug("Found a publishable post at {PostFilePath}", postMarkdown.FilePath);

            var currentPath = postMarkdown.FilePath;
            postMarkdown.Metadata.IsPublished = true;
            postMarkdown.Metadata.PublishedOn = DateTime.Now;
            postManager.Markdown.UpdateFile(postMarkdown);

            var publishedFilename = $"{DateTime.Now:yyyy-MM-dd}-{post}";
            var targetFolder = postManager.FileSystem.Path.Combine(postManager.Posts, $"{DateTime.Now:yyyy}");

            if (!postManager.FileSystem.Directory.Exists(targetFolder))
            {
                logger.LogDebug("Creating folder for post at {PostPublishTarget}", targetFolder);
                postManager.FileSystem.Directory.CreateDirectory(targetFolder);
            }

            var replacementPath = postManager.FileSystem.Path.Combine(targetFolder, publishedFilename);

            logger.LogInformation("Publishing {PublishedFileName} to {TargetFolder}", publishedFilename, targetFolder);
            postManager.FileSystem.File.Move(currentPath, replacementPath);
            postManager.FileSystem.File.Delete(currentPath);
            
            //await Command.RunAsync("git", "add --all", input.BaseDirectoryFlag, true);
            //ConsoleWriter.Write("Published file added to git index. Don't forget to commit and push to remote.");
        }
        else
        {
            logger.LogError("Could not find {Post} to publish", post);
        }
    }

}