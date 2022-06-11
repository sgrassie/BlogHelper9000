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
    DotNetRestore(parameters.SolutionFile, new DotNetRestoreSettings
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
    DotNetBuild(path.FullPath, new DotNetBuildSettings()
    {
        Configuration = parameters.Configuration,
        NoRestore = true
    });
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does<Parameters>((context, parameters) =>
{
    DotNetTest(parameters.SolutionFile, new DotNetTestSettings
    {
        Framework = "net6.0",
        NoBuild = true,
        NoRestore = true,
        Configuration = parameters.Configuration
    });
});

Task("Default").IsDependentOn("Build");
Task("Tests").IsDependentOn("Run-Unit-Tests");

RunTarget(Argument("target", "Default"));