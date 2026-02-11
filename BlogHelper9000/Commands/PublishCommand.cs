using BlogHelper9000.Core.Helpers;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

public sealed class PublishCommand : ICommand<Unit>
{
    [Parameter(Description = "The post to publish.")]
    public string Post { get; set; }

    public class Handler(ILogger<Handler> logger, PostManager postManager, TimeProvider timeProvider) 
        : ICommandHandler<PublishCommand, Unit>
    {
        public ValueTask<Unit> Handle(PublishCommand request, CancellationToken cancellationToken)
        {
            if (postManager.TryFindPost(request.Post, out var postMarkdown))
            {
                logger.LogDebug("Found a publishable post at {PostFilePath}", postMarkdown.FilePath);

                var currentPath = postMarkdown.FilePath;
                postMarkdown.Metadata.IsPublished = true;
                postMarkdown.Metadata.PublishedOn = timeProvider.GetLocalNow().DateTime;
                postManager.Markdown.UpdateFile(postMarkdown);

                var fileName = postManager.FileSystem.Path.GetFileName(postMarkdown.FilePath);
                var publishedFilename = $"{timeProvider.GetLocalNow().DateTime:yyyy-MM-dd}-{fileName}";
                var targetFolder = postManager.FileSystem.Path.Combine(postManager.Posts, $"{timeProvider.GetLocalNow().DateTime:yyyy}");

                if (!postManager.FileSystem.Directory.Exists(targetFolder))
                {
                    logger.LogDebug("Creating folder for post at {PostPublishTarget}", targetFolder);
                    postManager.FileSystem.Directory.CreateDirectory(targetFolder);
                }

                var replacementPath = postManager.FileSystem.Path.Combine(targetFolder, publishedFilename);

                logger.LogInformation("Publishing {PublishedFileName} to {TargetFolder}", publishedFilename,
                    targetFolder);
                postManager.FileSystem.File.Move(currentPath, replacementPath);
                postManager.FileSystem.File.Delete(currentPath);

                //await Command.RunAsync("git", "add --all", input.BaseDirectoryFlag, true);
                //ConsoleWriter.Write("Published file added to git index. Don't forget to commit and push to remote.");
            }
            else
            {
                logger.LogError("Could not find {Post} to publish", request.Post);
            }

            return default;
        }
    }
}
