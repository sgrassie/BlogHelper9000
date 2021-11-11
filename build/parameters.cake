public class Parameters
{
    public Parameters(ISetupContext context, string solutionFile)
    {
        SolutionFile = solutionFile;
        Target = context.TargetTask.Name;
        Configuration = context.Argument("configuration", "Debug");
    }

    public string SolutionFile { get; }
    public string Target { get; }
    public string Configuration { get; }
}