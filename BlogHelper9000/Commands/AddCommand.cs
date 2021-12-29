using System.Text;

namespace BlogHelper9000.Commands;

public class AddCommand : BaseCommand<BlogInput>
{
    public override bool Run(BlogInput input)
    {
        var postFile = CreatePostFile(input);
        AddYamlHeader(postFile, input);

        return true;
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

    private void AddYamlHeader(string path, BlogInput input)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine("---")
            .AppendLine($"title: {input.Title}")
            .AppendLine(TagsString(input.Tags))
            .AppendLine($"featured_image: {input.Image}")
            .AppendLine($"featured: {input.IsFeaturedFlag}")
            .AppendLine($"hidden: {input.IsHiddenFlag}")
            .AppendLine("---");

        File.AppendAllText(path, builder.ToString());

        string TagsString(string[] tags)
        {
            var result = string.Join(",", tags);
            return result;
        }
    }
}