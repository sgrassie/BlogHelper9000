using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.YamlParsing;
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

    public sealed class Handler(ILogger<Handler> logger, PostManager postManager)
        : ICommandHandler<AddCommand, Unit>
    {
        public ValueTask<Unit> Handle(AddCommand request, CancellationToken cancellationToken)
        {
            var postFile = CreatePostFilePath(request.Title, request.IsDraft);
            AddYamlHeader(postFile, request);

            logger.LogInformation("Added new post at {File}", postFile);
            return default;
        }

        private string CreatePostFilePath(string title, bool isDraft)
        {
            var newPostFilePath = isDraft
                ? postManager.CreateDraftPath(title)
                : postManager.CreatePostPath(title);

            return newPostFilePath;
        }

        private void AddYamlHeader(string filePath, AddCommand command)
        {
            var yamlHeader = new YamlHeader
            {
                Title = command.Title,
                Tags = [],//command.Tags.ToList(),
                FeaturedImage = command.FeaturedImage,
                IsFeatured = command.IsFeatured,
                IsHidden = command.IsHidden,
                IsPublished = !command.IsDraft
            };

            var yamlHeaderText = postManager.YamlConvert.Serialise(yamlHeader);

            postManager.FileSystem.File.AppendAllText(filePath, yamlHeaderText);
        }
    }
}
