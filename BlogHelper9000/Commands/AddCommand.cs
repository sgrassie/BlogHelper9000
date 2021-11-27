namespace BlogHelper9000.Commands;

public class AddCommand : BaseCommand<BlogInput>
{
    public override bool Run(BlogInput input)
    {
        if (!ProcessInput(input)) return false;

        var postFile = CreatePostFile(input);

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
}