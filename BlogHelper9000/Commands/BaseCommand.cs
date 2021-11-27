using System;
namespace BlogHelper9000.Commands
{
    public abstract class BaseCommand<TInput> : OaktonCommand<TInput>
        where TInput : BlogInput
    {
        private static string _draftsPath = Path.Combine(AppContext.BaseDirectory, "_drafts");
        private static string _postsPath = Path.Combine(AppContext.BaseDirectory, "_posts", DateTime.Now.Year.ToString());

        public string DraftsPath => _draftsPath;
        public string PostsPath => _postsPath;

        public override bool Execute(TInput input)
        {
            if (!ValidateInput(input)) return false;
            return Run(input);
        }

        public abstract bool Run(TInput input);

        private bool ValidateInput(TInput input)
        {
            if (!Directory.Exists(DraftsPath))
            {
                ConsoleWriter.Write(ConsoleColor.Red, "Unable to find blog _drafts folder");
                return false;
            }

            if (string.IsNullOrEmpty(input.Title))
            {
                ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have a title!");
                return false;
            }

            if (input.Tags == null || !input.Tags.Any())
            {
                ConsoleWriter.Write(ConsoleColor.Red, "The new post does not have any tags!");
                return false;
            }

            return true;
        }
    }
}