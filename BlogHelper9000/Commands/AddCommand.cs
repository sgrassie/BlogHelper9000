using BlogHelper9000.Core.Scheduling;
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
    [Option("series", "s", Description = "Add the new post to this schedule series.")]
    public string? Series { get; set; }
    [Option("week", "w", Description = "Schedule week number for the series entry.")]
    public int? Week { get; set; }
    [Option("publish-date", "p", Description = "Planned publish date (yyyy-MM-dd) for the series entry.")]
    public string? PublishDate { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IBlogService blogService, IScheduleService scheduleService)
        : ICommandHandler<AddCommand, Unit>
    {
        public ValueTask<Unit> Handle(AddCommand request, CancellationToken cancellationToken)
        {
            var tags = string.IsNullOrWhiteSpace(request.Tags)
                ? null
                : request.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var filePath = blogService.AddPost(request.Title, request.IsDraft, request.IsFeatured, request.IsHidden, request.FeaturedImage, tags);

            if (filePath is null)
            {
                logger.LogError("Could not add post '{Title}' — a post already exists at the target path", request.Title);
                return default;
            }

            logger.LogInformation("Added new post at {File}", filePath);

            if (!string.IsNullOrWhiteSpace(request.Series))
            {
                DateOnly? publishDate = DateOnly.TryParse(request.PublishDate, out var parsed) ? parsed : null;
                var entry = scheduleService.AddToSeries(request.Series, request.Title, Path.GetFileName(filePath),
                    week: request.Week, publishDate: publishDate, tags: request.Tags, notes: null);

                if (entry is null)
                    logger.LogWarning("'{File}' is already on the schedule — not added again", Path.GetFileName(filePath));
                else
                    logger.LogInformation("Scheduled as #{Position} in series '{Series}'", entry.Position, entry.Series);
            }

            return default;
        }
    }
}
