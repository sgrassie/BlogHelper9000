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
    .AddRoute<AddCommand>(
        pattern: "add {title} {tags?} --base-directory {basedirectory} --is-draft {draft?} --is-featured {isfeatured?} --is-hidden {ishidden?} --featured-image {featuredImage?}",
        description: "Adds a blog post.")
    .AddRoute<FixCommand>(
        pattern: "fix --base-directory {basedirectory} --status --description {description} --tags {tags}",
        description: "Enables various fixes to be applied to blog posts..")
    .Build();

return await app.RunAsync(args);
    