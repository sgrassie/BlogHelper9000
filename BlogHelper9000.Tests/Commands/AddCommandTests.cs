using BlogHelper9000.Commands;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Commands;

public class AddCommandTests 
{
    private IOptions<BlogHelperOptions> _options;
    
    public AddCommandTests()
    {
        _options = Options.Create(new BlogHelperOptions
        {
            BaseDirectory = "./blog"
        });
    }
    
    [Fact]
    public async Task Should_Output_Help()
    {
        // var console = new TestConsole();
        // var command = new AddCommand();
        //
        // await command.InvokeAsync("add -h", console);
        
       // console.Out.ToString().Should().Contain("add <title> [<tags>...] [options]");
    }
    
    [Theory]
    [InlineData("--draft", "Adds the post as a draft")]
    [InlineData("--is-featured", "Sets the post as a featured post")]
    [InlineData("--is-hidden", "Sets whether the post is hidden or not")]
    [InlineData("--featured-image", "Sets the featured image path")]
    [InlineData("--version", "Show version information")]
    public async Task Should_Output_Options(string optionName, string optionHelp)
    {
        // var console = new TestConsole();
        // var command = new AddCommand();
        // await command.InvokeAsync("add -h", console);
        //
        // var lines = console.AsLines()
        //     .Where(line => line.StartsWith("--")).ToList();
        //
        // lines.Should().Contain(x => x.StartsWith(optionName) && x.Contains(optionHelp));
    }
    
    [Fact]
    public async Task Should_Add_NewPost_AsDraft()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var command = new AddCommand
        {
            BaseDirectory = "/blog",
            Title = "New post in draft",
            IsDraft = true,
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, postManager);
        
        await sut.Handle(command, CancellationToken.None);

        fileSystem
            .File
            .Exists(fileSystem.Path.Combine(JekyllBlogFilesystemBuilder.Drafts, "new-post-in-draft.md"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_Add_NewPost_StraightToPosts()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder().BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        var command = new AddCommand
        {
            BaseDirectory = "/blog",
            Title = "New post in posts"
        };
        var sut = new AddCommand.Handler(NullLogger<AddCommand.Handler>.Instance, postManager);
        
        await sut.Handle(command, CancellationToken.None);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemBuilder.Posts, "new-post-in-posts.md"))
            .Should().BeTrue();
    }
}