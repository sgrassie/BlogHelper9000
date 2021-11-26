namespace BlogHelper9000.Commands;

public class AddInput
{
    public bool DraftFlag { get; set; }
    public string Title { get; set; }
    public string[] Tags { get; set; }
    public bool IsFeaturedFlag { get; set; } = false;
    public bool IsHiddenFlag { get; set; } = false;
    public string LayoutFlag { get; set; } = "post";
    public string? Image { get; set; }
}

public class AddCommand : OaktonCommand<AddInput>
{
    private static string _draftsPath = Path.Combine(AppContext.BaseDirectory, "_drafts");
    private static string _postsPath = Path.Combine(AppContext.BaseDirectory, "_posts", DateTime.Now.Year.ToString());

    public override bool Execute(AddInput input)
    {
        if (!ProcessInput(input)) return false;

        var postFile = CreatePostFile(input);

        return true;
    }

    private bool ProcessInput(AddInput input)
    {
        if (!Directory.Exists(_draftsPath))
        {
            ConsoleWriter.Write(ConsoleColor.Red, "Unable to find blog _drafts folder");
            return false;
        }

        if (string.IsNullOrEmpty(input.Title))
        {
            ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have a title!");
            return false;
        }

        if(input.Tags == null || !input.Tags.Any())
        {
            ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have any tags!");
            return false;
        }

        return true;
    }

    private string CreatePostFile(AddInput input)
    {
        var fileName = input.Title.Replace(" ", "-");

        if (input.DraftFlag)
        {
            var draftfilepath = Path.Combine(_draftsPath, fileName);
            CreateFile(draftfilepath);
            return draftfilepath;
        }

        var path = Path.Combine(_postsPath, fileName);
        CreateFile(path);
        return path;

        void CreateFile(string fullfilePath)
        {
            File.Create(fullfilePath);
        }
    }
}