#load "./build/parameters.cake"

Setup<Parameters>(context =>
{
    var parameters = new Parameters(context, "./BlogHelper9000.sln");
    return parameters;
});

Task("Clean")
.Does<Parameters>((context, parameters) =>
{
    CleanDirectories("./src/**/bin/" + parameters.Configuration);
    CleanDirectories("./src/**/obj");
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does<Parameters>((context, parameters) =>
{
    DotNetRestore(parameters.SolutionFile, new DotNetCoreRestoreSettings
    {
        Verbosity = DotNetCoreVerbosity.Minimal,
        Sources = new [] { "https://api.nuget.org/v3/index.json" },
    });
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does<Parameters>((context, parameters) =>
{
    // Build the solution.
    var path = MakeAbsolute(new DirectoryPath(parameters.SolutionFile));
    DotNetBuild(path.FullPath, new DotNetCoreBuildSettings()
    {
        Configuration = parameters.Configuration,
        NoRestore = true
    });
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .DoesForEach<Parameters, FilePath>(
        () => GetFiles("./src/**/*.Tests.csproj"),
        (parameters, project, context) =>
{
    foreach(var framework in new[] { "net6.0" })
    {
        DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
        {
            Framework = framework,
            NoBuild = true,
            NoRestore = true,
            Configuration = parameters.Configuration
        });
    }
});

Task("Default").IsDependentOn("Build");
Task("Tests").IsDependentOn("Run-Unit-Tests");

RunTarget(Argument("target", "Default"));