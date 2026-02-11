using BlogHelper9000;
using BlogHelper9000.Commands;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Reporters;
using Microsoft.Extensions.DependencyInjection;
using TimeWarp.Nuru;

var builder = NuruApp.CreateBuilder()
    .UseMicrosoftDependencyInjection()
    .AddConfiguration(args);

builder.Services.AddOptions<BlogHelperOptions>().BindConfiguration("BlogHelperOptions");
builder.Services.AddSingleton<IFileSystem, FileSystem>();
builder.Services.AddSingleton<InfoCommandReporter>();
builder.Services.AddSingleton<MarkdownHandler>();
builder.Services.AddSingleton<PostManager>();

NuruApp app = builder
    .DiscoverEndpoints()
    .Build();

return await app.RunAsync(args);
