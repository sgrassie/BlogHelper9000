using BlogHelper9000.Commands;
using BlogHelper9000.Reporters;
using Microsoft.Extensions.DependencyInjection;
using TimeWarp.Nuru;

NuruApp app = new NuruAppBuilder()
    .AddDependencyInjection(config => config.RegisterServicesFromAssemblyContaining<Program>())
    .AddConfiguration(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<InfoCommandReporter>();
    })
    .AddRoute<InfoCommand>(
        pattern: "info --base-directory {basedirectory}",
        description: "Provides information about the blog.")
    .Build();

return await app.RunAsync(args);
    