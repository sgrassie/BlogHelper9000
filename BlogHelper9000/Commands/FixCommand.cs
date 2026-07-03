using BlogHelper9000.Core.Services;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

public class FixCommand : ICommand<Unit>
{
    [Parameter(Description = "The query to use when searching for the image on Unsplash.")]
    public bool Status { get; set; }
    [Parameter(Description = "Fix description by copying from metadescription if it exists.")]
    public bool Description { get; set; }
        [Parameter(Description = "Fix tags by copying from category or categories if they exist.")]
    public bool Tags { get; set; }

    public class Handler(ILogger<Handler> logger, IBlogService blogService) : ICommandHandler<FixCommand, Unit>
    {
        public ValueTask<Unit> Handle(FixCommand request, CancellationToken cancellationToken)
        {
            blogService.FixMetadata(request.Status, request.Description, request.Tags);
            logger.LogInformation("Metadata fix completed");
            return default;
        }
    }
}
