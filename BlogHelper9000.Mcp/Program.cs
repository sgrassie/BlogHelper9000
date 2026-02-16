using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

// BlogHelper9000 core services — same DI wiring as the CLI & TUI
var baseDirectory = Environment.GetEnvironmentVariable("BLOG_BASE_DIRECTORY")
    ?? args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Directory.GetCurrentDirectory();

builder.Services.AddSingleton<IOptions<BlogHelperOptions>>(
    new OptionsWrapper<BlogHelperOptions>(new BlogHelperOptions { BaseDirectory = baseDirectory }));

builder.Services.AddSingleton<IFileSystem, FileSystem>();
builder.Services.AddSingleton<MarkdownHandler>();
builder.Services.AddSingleton<PostManager>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IBlogService, BlogService>();
builder.Services.AddSingleton<IUnsplashClient, UnsplashClient>();
builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();

// MCP server — stdio transport, auto-discover [McpServerTool] methods in this assembly
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "BlogHelper9000",
        Version = "1.0.0"
    };
})
.WithStdioServerTransport()
.WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
