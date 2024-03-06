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
        Verbosity = DotNetVerbosity.Minimal,
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
    .Does<Parameters>((context, parameters) =>
{
    DotNetTest(parameters.SolutionFile, new DotNetTestSettings
    {
        Configuration = parameters.Configuration,
        NoBuild = true,
        ArgumentCustomization = args => args
                                        .Append("/p:CollectCoverage=true /p:CoverletOutput=TestResults/ /p:CoverletOutputFormat=lcov")
    });
});

Task("PackedIt")
    .WithCriteria<Parameters>((context, parameters) => parameters.Configuration == "release")
    .Does<Parameters>((context, parameters) =>
    {
        var settings = new DotNetPackSettings
         {
             NoBuild = true,
             NoLogo = true,
             Configuration = "Release",
             OutputDirectory = "./releases/"
         };
        
         DotNetPack(parameters.SolutionFile, settings);
    });

Task("Default").IsDependentOn("Build");
Task("Tests")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");
Task("Pack")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("PackedIt");
    

RunTarget(Argument("target", "Default"));