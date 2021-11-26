using System;
namespace BlogHelper9000.Commands
{
    public abstract class BaseCommand<TInput> : OaktonCommand<TInput>
    {
        private static string _draftsPath = Path.Combine(AppContext.BaseDirectory, "_drafts");
        private static string _postsPath = Path.Combine(AppContext.BaseDirectory, "_posts", DateTime.Now.Year.ToString());

        public string DraftsPath => _draftsPath;
        public string PostsPath => _postsPath;

        public override bool Execute(TInput input)
        {
            return Run(input);
        }

        public abstract bool Run(TInput input);
    }
}

