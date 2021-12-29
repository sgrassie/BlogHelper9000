using System;
namespace BlogHelper9000.Commands
{
    public abstract class BaseCommand<TInput> : OaktonCommand<TInput>
        where TInput : BlogInput
    {
        protected string? DraftsPath { get; private set; }

        protected string? PostsPath { get; private set; }

        public override bool Execute(TInput input)
        {
            DraftsPath = Path.Combine(input.BaseDirectoryFlag, "_drafts");
            PostsPath = Path.Combine(input.BaseDirectoryFlag, "_posts");
            if (!ValidateInput(input)) return false;
            return Run(input);
        }

        protected abstract bool Run(TInput input);

        protected virtual bool ValidateInput(TInput input)
        {
            if (!Directory.Exists(DraftsPath))
            {
                ConsoleWriter.Write(ConsoleColor.Red, "Unable to find blog _drafts folder");
                return false;
            }
            
            if (!Directory.Exists(PostsPath))
            {
                ConsoleWriter.Write(ConsoleColor.Red, "Unable to find blog _posts folder");
                return false;
            }

            /*
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
            */

            return true;
        }
    }
}