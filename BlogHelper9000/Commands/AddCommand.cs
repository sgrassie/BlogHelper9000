using BlogHelper9000.Helpers;
using BlogHelper9000.YamlParsing;
using TimeWarp.Mediator;

namespace BlogHelper9000.Commands;

internal sealed class AddCommand : IRequest
{
    public string Title { get; set; }
    public string Tags { get; set; }
    public string BaseDirectory { get; set; }
    public bool IsDraft { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsHidden { get; set; }
    public string FeaturedImage { get; set; }


    public sealed class Handler(ILogger<Handler> logger, IFileSystem fileSystem)
        : IRequestHandler<AddCommand>
    {
        private PostManager _postManager;
        public Task Handle(AddCommand request, CancellationToken cancellationToken)
        {
            _postManager = new PostManager(fileSystem, request.BaseDirectory);
            var postFile = CreatePostFilePath(request.Title, request.IsDraft);
            AddYamlHeader(postFile, request);

            logger.LogInformation("Added new post at {File}", postFile);
            return Task.CompletedTask;
        }

        private string CreatePostFilePath(string title, bool isDraft)
        {
            var newPostFilePath = isDraft
                ? _postManager.CreateDraftPath(title)
                : _postManager.CreatePostPath(title);

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

            var yamlHeaderText = _postManager.YamlConvert.Serialise(yamlHeader);

            _postManager.FileSystem.File.AppendAllText(filePath, yamlHeaderText);
        }
    }
}