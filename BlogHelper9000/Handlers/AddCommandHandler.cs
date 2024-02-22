using System.CommandLine;
using System.IO.Abstractions;
using BlogHelper9000.Helpers;
using BlogHelper9000.YamlParsing;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Handlers;

public class AddCommandHandler(ILogger logger, PostManager postManager)
{
    public void Execute(string title, string[] tags, string featuredImage, bool isFeatured, bool isHidden, bool isDraft)
    {
        var postFile = CreatePostFilePath(title, isDraft);
        AddYamlHeader(postFile, title, tags, featuredImage, isFeatured, isHidden, isDraft);

        logger.LogInformation("Added new post at {File}", postFile);
    }

    private string CreatePostFilePath(string title, bool isDraft)
    {
        var newPostFilePath = isDraft
            ? postManager.CreateDraftPath(title)
            : postManager.CreatePostPath(title);

        return newPostFilePath;
    }

    private void AddYamlHeader(string path, string title, string[] tags, string featuredImage, bool isFeatured,
        bool isHidden, bool isDraft)
    {
        var yamlHeader = new YamlHeader
        {
            Title = title,
            Tags = tags.ToList(),
            FeaturedImage = featuredImage,
            IsFeatured = isFeatured,
            IsHidden = isHidden,
            IsPublished = !isDraft
        };

        var yamlHeaderText = postManager.YamlConvert.Serialise(yamlHeader);

        postManager.FileSystem.File.AppendAllText(path, yamlHeaderText);
    }
}