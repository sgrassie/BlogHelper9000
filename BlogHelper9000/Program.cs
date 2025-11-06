using BlogHelper9000;
using BlogHelper9000.Commands;
using BlogHelper9000.Helpers;
using BlogHelper9000.Reporters;
using Microsoft.Extensions.DependencyInjection;
using TimeWarp.Nuru;

NuruApp app = new NuruAppBuilder()
    .AddDependencyInjection(config => config.RegisterServicesFromAssemblyContaining<Program>())
    .AddConfiguration(args)
    .ConfigureServices((services, config) =>
    {
        services.AddOptions<BlogHelperOptions>().Bind(config.GetSection("BlogHelperOptions"));
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<InfoCommandReporter>();
        services.AddSingleton<MarkdownHandler>();
        services.AddSingleton<PostManager>();
    })
    .AddRoute<InfoCommand>(
        pattern: "info",
        description: "Provides information about the blog.")
    .AddRoute<AddCommand>(
        pattern: "add {title} {tags?} --base-directory {basedirectory} --is-draft {draft?} --is-featured {isfeatured?} --is-hidden {ishidden?} --featured-image {featuredImage?}",
        description: "Adds a blog post.")
    .AddRoute<FixCommand>(
        pattern: "fix --base-directory {basedirectory} --status --description {description} --tags {tags}",
        description: "Enables various fixes to be applied to blog posts..")
    .Build();

return await app.RunAsync(args);
    