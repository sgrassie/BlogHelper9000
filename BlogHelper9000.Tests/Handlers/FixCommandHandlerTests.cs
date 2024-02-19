using System.CommandLine.IO;
using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Handlers;

public class FixCommandHandlerTests
{
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
        
        var console = new TestConsole();
        var sut = new FixCommandHandler(new PostManager(fileSystem, "/blog"), console);
        
        sut.Execute(true, false, false, false);

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
        
        var console = new TestConsole();
        var sut = new FixCommandHandler(new PostManager(fileSystem, "/blog"), console);
        
        sut.Execute(true, false, false, false);

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
        
        var console = new TestConsole();
        var sut = new FixCommandHandler(new PostManager(fileSystem, "/blog"), console);
        
        sut.Execute(false, true, false, false);

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
        
        var console = new TestConsole();
        var sut = new FixCommandHandler(new PostManager(fileSystem, "/blog"), console);
        
        sut.Execute(false, false, true, false);

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
        
        var console = new TestConsole();
        var sut = new FixCommandHandler(new PostManager(fileSystem, "/blog"), console);
        
        sut.Execute(false, false, true, false);

        var contents = fileSystem.FileContentsAsArray("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain(x => x == "tags: ['C#','Coding','Plugin Manager']");
    }
}