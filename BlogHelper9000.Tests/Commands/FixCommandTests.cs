using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Commands;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Commands;

public class FixCommandTests 
{
    private IOptions<BlogHelperOptions> _options;
    
    public FixCommandTests()
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
        // var command = new FixCommand();
        // await command.InvokeAsync("fix -h", console);
        //
        // console.Out.ToString()
        //     .Should().Contain("fix [options]");
    }
    
    [Theory]
    [InlineData("-s, --status", "Fix the published status of a post")]
    [InlineData("-d, --description", "Fix the description of a post")]
    [InlineData("-t, --tags", "Fix the tags of a post")]
    public async Task Should_Output_Options(string optionName, string optionHelp)
    {
        // var console = new TestConsole();
        // var command = new FixCommand();
        // await command.InvokeAsync("fix -h", console);
        //
        // var lines = console.AsLines()
        //     .Where(line => line.StartsWith("-")).ToList();
        //
        // lines.Should().Contain(x => x.StartsWith(optionName) && x.Contains(optionHelp));
    }
    
    [Fact]
    public void Should_AddPublishedOnFromDateInFilename_WhenPublishedOnIsMissing()
    {
        var header = """
                     ---
                     layout: post
                     description: test post
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);

        var command = new FixCommand
        {
            BaseDirectory = "/blog",
            Status = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, postManager);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "published: 13/02/2024");
    }
    
    [Fact]
    public void Should_Update_PublishedOn_To_DateInFilename()
    {
        var header = """
                     ---
                     published: 01/01/2000
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        
        var command = new FixCommand
        {
            BaseDirectory = "/blog",
            Status = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, postManager);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "published: 13/02/2024");
    }
    
    [Fact]
    public void Should_Update_Description_ToUseCorrectProperty()
    {
        var header = """
                     ---
                     layout: post
                     metadescription: writing-a-generic-plugin-manager-in-c
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        
        var command = new FixCommand
        {
            BaseDirectory = "/blog",
            Description = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, postManager);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "description: writing-a-generic-plugin-manager-in-c");
    }
    
    [Fact]
    public void Should_Update_UpdateCategory_ToUseCorrectTagProperty()
    {
        var header = """
                     ---
                     layout: post
                     category: C#,C#,Coding,Plugin Manager
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        
        var command = new FixCommand
        {
            BaseDirectory = "/blog",
            Tags = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, postManager);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "tags: ['C#','Coding','Plugin Manager']");
    }
    
    [Fact]
    public void Should_Update_UpdateCategories_ToUseCorrectTagProperty()
    {
        var header = """
                     ---
                     layout: post
                     categories: C#,C#,Coding,Plugin Manager
                     ---
                     """;
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        
        var postManager = new PostManager(fileSystem, new MarkdownHandler(fileSystem), _options);
        
        var command = new FixCommand
        {
            BaseDirectory = "/blog",
            Tags = true
        };
        var sut = new FixCommand.Handler(NullLogger<FixCommand.Handler>.Instance, postManager);

        sut.Handle(command, CancellationToken.None);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "tags: ['C#','Coding','Plugin Manager']");
    }
}