using BlogHelper9000;
using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using BlogHelper9000.Reporters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeWarp.Nuru;

var builder = NuruApp.CreateBuilder()
    .UseMicrosoftDependencyInjection()
    .AddConfiguration(args);

builder.Services.AddOptions<BlogHelperOptions>().BindConfiguration("BlogHelperOptions");
builder.Services.AddSingleton<IFileSystem, FileSystem>();
builder.Services.AddSingleton<InfoCommandReporter>();
builder.Services.AddSingleton<MarkdownHandler>();
builder.Services.AddSingleton<PostManager>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IBlogService, BlogService>();
builder.Services.AddSingleton(_ =>
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Accept-Version", "v1");
    return client;
});
builder.Services.AddSingleton<IUnsplashClient>(sp => new UnsplashClient(
    sp.GetRequiredService<HttpClient>(),
    sp.GetRequiredService<IFileSystem>(),
    sp.GetRequiredService<ILoggerFactory>().CreateLogger<UnsplashClient>()));
builder.Services.AddSingleton<IImageProcessor>(sp => new ImageProcessor(
    sp.GetRequiredService<ILoggerFactory>().CreateLogger<ImageProcessor>(),
    sp.GetRequiredService<PostManager>()));

NuruApp app = builder
    .DiscoverEndpoints()
    .Build();

return await app.RunAsync(args);
