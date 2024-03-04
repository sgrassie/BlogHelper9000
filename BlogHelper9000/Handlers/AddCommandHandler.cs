using System.CommandLine;
using System.IO.Abstractions;
using BlogHelper9000.Commands;
using BlogHelper9000.Helpers;
using BlogHelper9000.YamlParsing;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Handlers;

public class AddCommandHandler(ILogger logger, PostManager postManager)
{
    public void Execute(AddOptions options)
    {
        var postFile = CreatePostFilePath(options.Title, options.IsDraft);
        AddYamlHeader(postFile, options);

        logger.LogInformation("Added new post at {File}", postFile);
    }

    private string CreatePostFilePath(string title, bool isDraft)
    {
        var newPostFilePath = isDraft
            ? postManager.CreateDraftPath(title)
            : postManager.CreatePostPath(title);

        return newPostFilePath;
    }

    private void AddYamlHeader(string filePath, AddOptions options)
    {
        var yamlHeader = new YamlHeader
        {
            Title = options.Title,
            Tags = options.Tags.ToList(),
            FeaturedImage = options.FeaturedImage,
            IsFeatured = options.IsFeatured,
            IsHidden = options.IsHidden,
            IsPublished = !options.IsDraft
        };

        var yamlHeaderText = postManager.YamlConvert.Serialise(yamlHeader);

        postManager.FileSystem.File.AppendAllText(filePath, yamlHeaderText);
    }
}