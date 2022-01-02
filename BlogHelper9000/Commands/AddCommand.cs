using System.Text;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Commands;

public class AddCommand : BaseCommand<BaseInput>
{
    public AddCommand()
    {
        Usage("Add new post").Arguments(x => x.Title, x => x.Tags);
    }
    protected override bool Run(BaseInput input)
    {
        var postFile = CreatePostFilePath(input);
        AddYamlHeader(postFile, input);

        return true;
    }

    protected override bool ValidateInput(BaseInput input)
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

    private string CreatePostFilePath(BaseInput input)
    {
        var fileName = input.Title.Replace(" ", "-");
        var newPostFilePath = input.DraftFlag
            ? Path.ChangeExtension(Path.Combine(DraftsPath, fileName), "md")
            : Path.ChangeExtension(Path.Combine(PostsPath, fileName), "md");

        return newPostFilePath;
    }

    private static void AddYamlHeader(string path, BaseInput input)
    {
        var yamlHeader = new YamlHeader
        {
            Title = input.Title,
            Tags = input.Tags.ToList(),
            FeaturedImage = input.ImageFlag,
            IsFeatured = input.IsFeaturedFlag,
            IsHidden = input.IsHiddenFlag
        };

        if (input.DraftFlag)
        {
            yamlHeader.IsPublished = false;
        }

        var yamlHeaderText = YamlConvert.Serialise(yamlHeader);

        File.AppendAllText(path, yamlHeaderText);
    }
}