using BlogHelper9000.Commands.Inputs;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Commands;

public class AddCommand : AsyncBaseCommand<AddInput>
{
    public AddCommand()
    {
        Usage("Add new post").Arguments(x => x.Title, x => x.Tags);
    }
    protected override async Task<bool> Run(AddInput input)
    {
        var postFile = CreatePostFilePath(input);
        AddYamlHeader(postFile, input);
        
        //ConsoleWriter.Write("Added new file {0} as draft", postFile);

        if (input.GitAddFlag)
        {
            //ConsoleWriter.Write("Adding new post to git...");
            var (standardOut, standardError) = await Command.ReadAsync("git", "add --all", input.BaseDirectoryFlag);
            //ConsoleWriter.Write(standardOut);
            //ConsoleWriter.Write(@"Don't forget to commit: git commit -m ""Added new post""");
        }

        return true;
    }

    protected override bool ValidateInput(AddInput input)
    {
        if (string.IsNullOrEmpty(input.Title))
        {
            //ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have a title!");
            return false;
        }

        if (!input.Tags.Any())
        {
            //ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have any tags!");
            return false;
        }

        return base.ValidateInput(input);
    }

    private string CreatePostFilePath(AddInput input)
    {
        var fileName = input.Title.Replace(" ", "-").ToLowerInvariant();
        var newPostFilePath = input.DraftFlag
            ? Path.ChangeExtension(Path.Combine(DraftsPath, fileName), "md")
            : Path.ChangeExtension(Path.Combine(PostsPath, fileName), "md");

        return newPostFilePath;
    }

    private static void AddYamlHeader(string path, AddInput input)
    {
        var yamlHeader = new YamlHeader
        {
            Title = input.Title,
            Tags = input.Tags.ToList(),
            FeaturedImage = input.ImageFlag,
            IsFeatured = input.IsFeaturedFlag,
            IsHidden = input.IsHiddenFlag,
            IsPublished = false
        };

        if (input.DraftFlag)
        {
            yamlHeader.IsPublished = false;
        }

        var yamlHeaderText = YamlConvert.Serialise(yamlHeader);

        File.AppendAllText(path, yamlHeaderText);
    }
}