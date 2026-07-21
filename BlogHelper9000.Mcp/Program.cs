using System.Reflection;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

// stdio MCP servers exchange JSON-RPC on stdout — logs must never share that stream,
// or a log line can desynchronize the client mid-frame.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

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
builder.Services.AddSingleton<IPostSearchService, PostSearchService>();
builder.Services.AddSingleton<IScheduleService, ScheduleService>();
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

// MCP server — stdio transport, auto-discover [McpServerTool] methods in this assembly
var serverVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

const string serverInstructions = """
    BlogHelper9000 manages a Jekyll blog at a configured base directory. Drafts live in
    _drafts/; published posts live in _posts/<year>/. A post is identified by its filename
    (e.g. 'my-post.md') or a path — bare filenames are resolved against _drafts/ then
    _posts/, and paths outside the blog root are rejected.

    Typical workflow: get_blog_info -> list_drafts/list_posts -> search_posts (check for
    prior coverage / find posts to link to) -> add_post -> get_post -> publish_post ->
    add_featured_image.

    Conventions: titles are slugified to lowercase-hyphenated filenames; tags are supplied
    as a comma-separated string (e.g. 'csharp, dotnet'); in front matter, 'published:' holds
    a date (or a draft/true/false placeholder before it is set), and the separate boolean
    publish flag is tracked internally — you do not need to set it directly.

    Discovery tools: search_posts answers "have I written about X?" and internal-linking
    research with a full-text and/or tag search across posts and drafts, returning snippets.
    list_drafts is a triage view of _drafts/ — title, word count, last-modified, schedule
    series/slot, and readiness flags (e.g. TODO/FIXME markers) for each draft, so you can
    see what's close to done without opening every file. get_tags returns the tag taxonomy
    with usage counts and casing variants across the blog, plus the normalisation rules
    fix_metadata applies — use it before tagging a new post to stay consistent with existing
    tags.

    Caution: fix_metadata rewrites every post under _posts/ in one call — call it with
    dryRun=true first to preview the change before applying it. add_featured_image performs
    network calls to Unsplash and requires credentials configured on the host machine.

    The publishing schedule lives in a SQLite database at .bloghelper.db in the blog root
    (dot-prefixed so Jekyll does not copy it into the generated site). It records post
    series and per-post schedule entries keyed by draft filename. Schedule tools:
    list_series, get_series, get_schedule_stats, get_next_scheduled_post,
    add_post_to_series, mark_schedule_entry_published. publish_post automatically ticks
    the matching schedule entry, so mark_schedule_entry_published is only needed for
    posts published by other means. Scheduled-publishing workflow: get_schedule_stats ->
    get_next_scheduled_post -> get_post (review) -> publish_post. If the schedule tools
    report that no database exists, the user must create it with
    'bloghelper schedule-import <xlsx>'.
    """;

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "BlogHelper9000",
        Version = serverVersion
    };
    options.ServerInstructions = serverInstructions;
})
.WithStdioServerTransport()
.WithToolsFromAssembly();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    var fileSystem = startupScope.ServiceProvider.GetRequiredService<IFileSystem>();
    var logger = startupScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var looksLikeJekyllBlog = fileSystem.Directory.Exists(fileSystem.Path.Combine(baseDirectory, "_posts"))
        || fileSystem.Directory.Exists(fileSystem.Path.Combine(baseDirectory, "_drafts"));

    if (!looksLikeJekyllBlog)
    {
        logger.LogWarning(
            "'{BaseDirectory}' does not look like a Jekyll blog (_posts/_drafts not found). " +
            "Set BLOG_BASE_DIRECTORY or pass the blog path as the first argument.",
            baseDirectory);
    }
}

await app.RunAsync();

internal sealed partial class Program;
