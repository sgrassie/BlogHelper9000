using BlogHelper9000.Core.Services;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("add", Description =  "Add a new post")]
public sealed class AddCommand : ICommand<Unit>
{
    [Parameter(Description = "The title of the post to add.")]
    public string Title { get; set; }
    [Parameter(Description = "Comma-separated list of tags for the post.")]
    public string Tags { get; set; }
    [Option("is-draft", "d", Description = "Indicates whether the post is a draft.")]
    public bool IsDraft { get; set; }
    [Option("is-featured", "f", Description = "Indicates whether the post is featured.")]
    public bool IsFeatured { get; set; }
    [Option("is-hidden", "h", Description = "Indicates whether the post is hidden.")]
    public bool IsHidden { get; set; }
    [Option("featured-image", "i", Description = "The path to the featured image for the post.")]
    public string FeaturedImage { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IBlogService blogService)
        : ICommandHandler<AddCommand, Unit>
    {
        public ValueTask<Unit> Handle(AddCommand request, CancellationToken cancellationToken)
        {
            var tags = string.IsNullOrWhiteSpace(request.Tags)
                ? null
                : request.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var filePath = blogService.AddPost(request.Title, request.IsDraft, request.IsFeatured, request.IsHidden, request.FeaturedImage, tags);

            if (filePath is null)
                logger.LogError("Could not add post '{Title}' — a post already exists at the target path", request.Title);
            else
                logger.LogInformation("Added new post at {File}", filePath);

            return default;
        }
    }
}
