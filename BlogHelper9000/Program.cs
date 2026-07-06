using BlogHelper9000;
using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using BlogHelper9000.Reporters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeWarp.Nuru;

var builder = NuruApp.CreateBuilder()
    .UseMicrosoftDependencyInjection()
    .AddConfiguration(args);

// Nuru's source generator only enables DI when registrations go through
// ConfigureServices; touching builder.Services directly throws at startup.
builder.ConfigureServices(services =>
{
    // Nuru's generator inlines this lambda into a static method, so it cannot capture
    // anything from Program: the IConfiguration for options binding is rebuilt here
    // from the same sources the generated host uses.
    services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(Environment.GetCommandLineArgs().Where(a => a.Contains('=')).ToArray())
        .Build());
    services.AddLogging(logging => logging.AddConsole());
    services.AddOptions<BlogHelperOptions>().BindConfiguration("BlogHelperOptions");
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<InfoCommandReporter>();
    services.AddSingleton<ScheduleReporter>();
    services.AddSingleton<MarkdownHandler>();
    services.AddSingleton<PostManager>();
    services.AddSingleton<TimeProvider>(TimeProvider.System);
    services.AddSingleton<IBlogService, BlogService>();
    services.AddSingleton<IScheduleService, ScheduleService>();
    services.AddSingleton(_ =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept-Version", "v1");
        return client;
    });
    services.AddSingleton<IUnsplashClient>(sp => new UnsplashClient(
        sp.GetRequiredService<HttpClient>(),
        sp.GetRequiredService<IFileSystem>(),
        sp.GetRequiredService<ILoggerFactory>().CreateLogger<UnsplashClient>()));
    services.AddSingleton<IImageProcessor>(sp => new ImageProcessor(
        sp.GetRequiredService<ILoggerFactory>().CreateLogger<ImageProcessor>(),
        sp.GetRequiredService<PostManager>()));
});

NuruApp app = builder
    .DiscoverEndpoints()
    .Build();

return await app.RunAsync(args);
