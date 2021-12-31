using System.Text;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Commands;

public class AddCommand : BaseCommand<BlogInput>
{
    protected override bool Run(BlogInput input)
    {
        var postFile = CreatePostFile(input);
        AddYamlHeader(postFile, input);

        return true;
    }

    protected override bool ValidateInput(BlogInput input)
    {
        if (string.IsNullOrEmpty(input.Title))
        {
            ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have a title!");
            return false;
        }

        if (!input.Tags.Any())
        {
            ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have any tags!");
            return false;
        }

        return base.ValidateInput(input);
    }

    private string CreatePostFile(BlogInput input)
    {
        var fileName = input.Title.Replace(" ", "-");

        if (input.DraftFlag)
        {
            var draftfilepath = Path.Combine(DraftsPath, fileName);
            CreateFile(draftfilepath);
            return draftfilepath;
        }

        var path = Path.Combine(PostsPath, fileName);
        CreateFile(path);
        return path;

        void CreateFile(string fullfilePath)
        {
            File.Create(fullfilePath);
        }
    }

    private static void AddYamlHeader(string path, BlogInput input)
    {
        var yamlHeader = new YamlHeader
        {
            Title = input.Title,
            Tags = input.Tags.ToList(),
            FeaturedImage = input.Image,
            IsFeatured = input.IsFeaturedFlag,
            IsHidden = input.IsHiddenFlag
        };

        var yamlHeaderText = YamlConvert.Serialise(yamlHeader);

        File.AppendAllText(path, yamlHeaderText);
    }
}