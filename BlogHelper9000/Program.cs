using BlogHelper9000.Commands;
using BlogHelper9000.Reporters;
using Microsoft.Extensions.DependencyInjection;
using TimeWarp.Mediator;
using TimeWarp.Nuru;

NuruApp app = new NuruAppBuilder()
    .AddDependencyInjection()
    .AddConfiguration(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<InfoCommandReporter>();

        services.AddSingleton<IRequestHandler<InfoCommand>>();
    })
    .AddRoute<InfoCommand>("info --base-directory {directory}")
    .Build();

return await app.RunAsync(args);
    