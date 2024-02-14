using BlogHelper9000.ObsoleteOaktonCommands.Inputs;

namespace BlogHelper9000.ObsoleteOaktonCommands;

internal class BaseHelper<TInput> where TInput : BaseInput
{
    public string DraftsPath { get; }

    public string PostsPath { get;  }

    private BaseHelper(TInput input)
    {
        DraftsPath = Path.Combine(input.BaseDirectoryFlag, "_drafts");
        PostsPath = Path.Combine(input.BaseDirectoryFlag, "_posts");
    }

    public static BaseHelper<TInput> Initialise(TInput input)
    {
        return new BaseHelper<TInput>(input);
    }

    public bool ValidateInput(TInput input)
    {
        if (!Directory.Exists(DraftsPath))
        {
            //ConsoleWriter.Write(ConsoleColor.Red, "Unable to find blog _drafts folder");
            return false;
        }

        if (!Directory.Exists(PostsPath))
        {
            //ConsoleWriter.Write(ConsoleColor.Red, "Unable to find blog _posts folder");
            return false;
        }

        return true;
    }
}