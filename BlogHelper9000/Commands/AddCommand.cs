using System.Text;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Commands;

public class AddCommand : BaseCommand<BlogInput>
{
    public AddCommand()
    {
        Usage("Add new post").Arguments(x => x.Title, x => x.Tags);
    }
    protected override bool Run(BlogInput input)
    {
        var postFile = CreatePostFilePath(input);
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

    private string CreatePostFilePath(BlogInput input)
    {
        var fileName = input.Title.Replace(" ", "-");
        var newPostFilePath = input.DraftFlag
            ? Path.ChangeExtension(Path.Combine(DraftsPath, fileName), "md")
            : Path.ChangeExtension(Path.Combine(PostsPath, fileName), "md");

        return newPostFilePath;
    }

    private static void AddYamlHeader(string path, BlogInput input)
    {
        var yamlHeader = new YamlHeader
        {
            Title = input.Title,
            Tags = input.Tags.ToList(),
            FeaturedImage = input.ImageFlag,
            IsFeatured = input.IsFeaturedFlag,
            IsHidden = input.IsHiddenFlag
        };

        var yamlHeaderText = YamlConvert.Serialise(yamlHeader);

        File.AppendAllText(path, yamlHeaderText);
    }
}